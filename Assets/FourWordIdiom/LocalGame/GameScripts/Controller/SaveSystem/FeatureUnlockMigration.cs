using UnityEngine;

/// <summary>
/// 功能解锁持久化迁移（bug111 / bug112 / bug113 / bug114）
///
/// 背景：老版本的功能解锁是「现算」的——用当前等级、每日重置字段（timerePuzzleid/timePuzzlecount）、
///       会话字段（isJoinedZenRank）判断。新版本把门槛抬高（蝶园 8→12、限时 10→15、禅 21→26）之后，
///       老用户会出现：
///         111 入口显示已解锁（IsOpen 有 legacy 豁免），但结算发蝉蛹仍走硬门槛 → 不给蛹；
///         112 广告追踪引导对新字段 adTrackingGuideShown 默认为 false 的老存档再次弹出；
///         113 莲心榜结算时 isJoinedZenRank 被置 false → 入口从"已解锁"退回"隐藏"；
///         114 灯笼解锁依赖每天清零的限时进度字段 → 重启/跨天变回锁定。
///
/// 第二批（schema 2）：每日任务、锦鲤竞速同样中招
///         115 每日任务门槛 16→31，且老用户豁免用的是竞速周时间窗 opentime（从没打过竞速 = 空）
///             → 17~30 关老用户入口消失，且 UpdateTaskProgress 直接 return，任务进度不再累计；
///         116 竞速门槛 21→39，老用户豁免用的是 roundstarttime / aiSaveDatas（每局结束 ResetFishData 清空）
///             → 22~38 关老用户打完一局就回锁，比赛进度也不再计入。
///
/// 本类做唯一一件事：在新版本第一次运行时，把老用户「曾经解锁过」这件事固化成 UserData 上的持久标记，
/// 之后所有功能判定都只认「等级达标 OR 持久标记」，不再读可变字段。
///
/// 幂等：靠 unlockSchemaVersion 保证只跑一次；缓存版本号随 UserData（含云端）一起走，
///       因此「新装机本地空档」与「之后从服务器拉回老账号」两条路径都能各自正确迁移。
/// </summary>
public static class FeatureUnlockMigration
{
    /// <summary>当前迁移版本。以后再加新功能解锁，递增并补一段迁移即可。</summary>
    private const int CurrentSchema = 2;

    // 老版本的"展示/解锁"门槛（迁移判据，不要跟着 AppGameSettings 走：
    // AppGameSettings 里的数值会随版本调整，这里要的是"老版本玩家当时能看到什么"）。
    private const int LegacyButterflyShowLevel = 8;
    private const int LegacyTimeLimitShowLevel = 10;
    private const int LegacyZenShowLevel = 21;
    // 旧版本 UnlockRequirements：每日任务 16、捕鱼（锦鲤竞速）21
    private const int LegacyDailyTaskUnlockLevel = 16;
    private const int LegacyFishUnlockLevel = 21;
    private const string DefaultZenLevel = "ZenState01";

    /// <summary>
    /// 保证当前 UserData 已完成解锁迁移。可在任意时机重复调用。
    /// </summary>
    /// <param name="user">要迁移的存档对象（本地读档 / 服务器下发的那一份）</param>
    /// <param name="isVersionUpgraded">服务端登录返回的"本次为版本升级"标记</param>
    /// <param name="persist">
    /// 是否立即落盘。登录完成后（LoadingController）传 true；
    /// 读本地存档阶段（LoadPlayerProfile）传 false —— 那里写文件会凭空造出存档，
    /// 影响 UserData.LocalDataIsNull() 对新号的判断，交给后续正常保存即可
    /// （内存里的 unlockSchemaVersion 已经置上，下次 SaveData 自然带落）。
    /// </param>
    public static void EnsureMigrated(UserData user, bool isVersionUpgraded, bool persist = true)
    {
        if (user == null) return;
        if (user.unlockSchemaVersion >= CurrentSchema) return;

        int maxStage = Mathf.Max(user.CurrentHexStage, user.CurrentChessStage);

        // 老账号判定：只要有任何真实进度痕迹就算老号。
        // ★ 不能用 IsFirstLaunch（隐私页就置 false 了）、也不能用 Rigister（新号上报用户属性时也会置 true）。
        bool isOldAccount = isVersionUpgraded
                            || maxStage > AppGameSettings.FirstLevel
                            || user.TutorialProgress > 0
                            || user.TotalEarnedGold > 0
                            || user.isJoinedZenRank
                            || (!string.IsNullOrEmpty(user.Zenlevel) && user.Zenlevel != DefaultZenLevel);

        if (isOldAccount)
        {
            var bf = GameDataManager.Instance != null ? GameDataManager.Instance.ButterflyData : null;
            int butterflyCount = bf != null ? bf.butterflies.Count : 0;
            int pupaCount = bf != null ? bf.currPupa : 0;

            // 说明：这里的迁移只对 schema==0 的存档跑一次。
            //   - 之后所有新装机的玩家，首启（1 关）就会把 schema 置 1，永远不会走到旧门槛；
            //   - 所以 schema==0 + 有进度 = 一定是本次改动之前就存在的老存档，可以放心按旧版本门槛继承。
            //
            // 111 蝶园：收集过蝴蝶 / 有蛹 → 一定解锁；老版本 8 关就展示过 → 同样继承（旧包玩家当时能看到已解锁入口）
            user.butterflyGardenUnlocked = user.butterflyGardenUnlocked
                                           || butterflyCount > 0
                                           || pupaCount > 0
                                           || maxStage >= LegacyButterflyShowLevel;

            // 114 限时灯笼：用过活动（当天字段还没被每日重置清掉）→ 一定解锁；旧版本 10 关展示过 → 继承
            user.timeLimitUnlocked = user.timeLimitUnlocked
                                     || user.timerePuzzleid > 0
                                     || user.timePuzzlecount > 0
                                     || maxStage >= LegacyTimeLimitShowLevel;

            // 113 莲心榜：加入过榜单 / 有非默认段位 → 一定解锁；旧版本 21 关展示过 → 继承
            user.zenRankUnlocked = user.zenRankUnlocked
                                   || user.isJoinedZenRank
                                   || (!string.IsNullOrEmpty(user.Zenlevel) && user.Zenlevel != DefaultZenLevel)
                                   || maxStage >= LegacyZenShowLevel;

            // 112 广告追踪引导：老号视为已看过，只对新号弹
            user.adTrackingGuideShown = true;

            // ===== schema 2 追加：每日任务 / 锦鲤竞速 =====
            // 旧门槛 16 / 21 → 新门槛 31 / 39，老玩家 17~30、22~38 关会集体"被降级"。
            // 痕迹判据：
            //   每日任务 → 有任务存档（打过任务进度）；旧版本 16 关就解锁
            //   竞速     → curround>1 / matchCount>0 / 有比赛进度（cliam·rank·Puzzleprogress 都随回合重置，不能当依据）
            int taskSaveCount = user.taskSaveDatas != null ? user.taskSaveDatas.Count : 0;
            var fish = GameDataManager.Instance != null ? GameDataManager.Instance.FishUserSave : null;
            int fishRound = fish != null ? fish.curround : 0;
            int fishMatch = fish != null ? fish.matchCount : 0;

            user.dailyTaskUnlocked = user.dailyTaskUnlocked
                                     || taskSaveCount > 0
                                     || maxStage >= LegacyDailyTaskUnlockLevel;

            user.fishRaceUnlocked = user.fishRaceUnlocked
                                    || fishRound > 1
                                    || fishMatch > 0
                                    || maxStage >= LegacyFishUnlockLevel;

            Debug.Log($"[Unlock] 迁移完成 schema={CurrentSchema} upgraded={isVersionUpgraded} " +
                      $"firstVersion={user.first_version} stage=hex{user.CurrentHexStage}/chess{user.CurrentChessStage} " +
                      $"butterfly={user.butterflyGardenUnlocked}(bf{butterflyCount},pupa{pupaCount}) " +
                      $"timeLimit={user.timeLimitUnlocked}(id{user.timerePuzzleid},cnt{user.timePuzzlecount}) " +
                      $"zenRank={user.zenRankUnlocked}(joined{user.isJoinedZenRank},level{user.Zenlevel}) " +
                      $"dailyTask={user.dailyTaskUnlocked}(tasks{taskSaveCount}) " +
                      $"fishRace={user.fishRaceUnlocked}(round{fishRound},match{fishMatch})");
        }
        else
        {
            Debug.Log($"[Unlock] 新账号，跳过老用户迁移 schema={CurrentSchema} stage=hex{user.CurrentHexStage}/chess{user.CurrentChessStage}");
        }

        user.unlockSchemaVersion = CurrentSchema;

        // 登录完成后的路径直接落本地文件；读档阶段交给后续正常保存（避免凭空造存档）。
        // 注意不要用 CommitGameData：它会上传服务器并额外写 7 份数据，迁移只改主存档。
        if (persist) user.SaveData();
    }
}
