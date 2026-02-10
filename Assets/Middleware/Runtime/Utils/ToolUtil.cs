using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Middleware
{
    public class ToolUtil
    {
        private static string _curBundle;
        //获取设备本地语言
        public static string GetLanguageBundle()
        {
            if (string.IsNullOrEmpty(_curBundle))
            {
                // Debug.Log($"++++++++当前语言：{Application.systemLanguage}");
                // switch (Application.systemLanguage)
                // {
                //     default:
                        _curBundle = "ChineseSimplified";
                //         break;
                // }    
            }
            return _curBundle.ToLower();
        }
        
        //获取表格中的文件
        public static Dictionary<string,string> ParseCvsLanguage(string csvFile,string name)
        {
            var dic = new Dictionary<string, string>();
            if (csvFile == null)
            {
                Debug.LogError(name + ": 找不到对应的词典.");
                return dic;
            }
            
            var lines = csvFile.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var headers = lines[0].Split(',');
            var rawId = -1;
            for (var i = 0; i < headers.Length; i++)
            {
                var langCode = headers[i].ToLower().Trim();
                // Debug.Log(langCode + " , " + GetLanguageBundle());
                if (langCode.Equals(GetLanguageBundle()))
                    rawId = i;
            }

            if (rawId == -1)
            {
                rawId = 1;
                Debug.LogError($"{name}: 没找到{GetLanguageBundle()}的词组，用{headers[rawId].ToLower()}代替.");
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseLine(lines[i]);
                if(values.Count <= rawId) continue;
                var key = values[0];
                dic[key] = values[rawId];        
            }

            return dic;
        }
        /// <summary>
        /// 先把完整 CSV 记录（可含引号内换行）切出来，再按字段拆
        /// </summary>
        public static Dictionary<string,string> ReadCvsLanguage(string csvFile, string name)
        {
            var dic = new Dictionary<string, string>();
            if (csvFile == null)
            {
                Debug.LogError(name + ": 找不到对应的词典.");
                return dic;
            }
            var table = new List<List<string>>();

            // 1. 状态机：按「引号对」切分记录（可含换行）
            var records = SplitRecords(csvFile);

            // 2. 逐记录拆字段
            foreach (var rec in records)
                table.Add(SplitFields(rec));

            var headers = table[0];
            var rawId = -1;
            for (var i = 0; i < headers.Count; i++)
            {
                var langCode = headers[i].ToLower().Trim();
                // Debug.Log(langCode + " , " + GetLanguageBundle());
                if (langCode.Equals(GetLanguageBundle()))
                    rawId = i;
            }
            if (rawId == -1)
            {
                rawId = 1;
                Debug.LogError($"{name}: 没找到{GetLanguageBundle()}的词组，用{headers[rawId].ToLower()}代替.");
            }

            for (int i = 1; i < table.Count; i++)
            {
                var values = table[i];
                if(values.Count <= rawId) continue;
                var key = values[0];
                dic[key] = values[rawId];        
            }
            return dic;
        }
   
        /*========== 第 1 步：按记录切分（可含引号内换行）==========*/
        private static List<string> SplitRecords(string text)
        {
            var records = new List<string>();
            var sb      = new StringBuilder();
            bool inQuotes = false;
            int i = 0, len = text.Length;

            while (i < len)
            {
                char ch = text[i];

                if (ch == '"')
                {
                    inQuotes = !inQuotes;   // 进入/退出引号区
                    sb.Append(ch);
                    i++;
                }
                else if (ch == '\n' || ch == '\r')
                {
                    if (!inQuotes)          // 只有在「非引号区」的换行才是「记录分隔符」
                    {
                        // 跳过连续换行
                        while (i < len && (text[i] == '\n' || text[i] == '\r')) i++;
                        records.Add(sb.ToString());
                        sb.Clear();
                    }
                    else                    // 引号内换行 → 原样保留
                    {
                        sb.Append(ch);
                        i++;
                    }
                }
                else
                {
                    sb.Append(ch);
                    i++;
                }
            }
            // 最后一行
            if (sb.Length > 0) records.Add(sb.ToString());
            return records;
        }
        /*========== 第 2 步：按字段拆（逗号 + 引号）==========*/
        private static List<string> SplitFields(string record)
        {
            var fields = new List<string>();
            var sb     = new StringBuilder();
            bool inQuotes = false;
            int i = 0, len = record.Length;

            while (i < len)
            {
                char ch = record[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < len && record[i + 1] == '"') // 转义引号 ""
                    {
                        sb.Append('"');
                        i += 2;
                    }
                    else
                    {
                        inQuotes = !inQuotes;   // 进入/退出引号区
                        i++;
                    }
                }
                else if (ch == ',' && !inQuotes) // 分隔符（非引号内）
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                    i++;
                }
                else                                // 普通字符（含引号内换行）
                {
                    sb.Append(ch);
                    i++;
                }
            }
            // 最后一列
            fields.Add(sb.ToString());
            return fields;
        }
        /// <summary>
        /// 解析一行 CSV，支持双引号包裹、引号内逗号、转义引号
        /// </summary>
        public static List<string> ParseLine(string line)
        {
            var result  = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                char next = i + 1 < line.Length ? line[i + 1] : '\0';

                if (ch == '"')
                {
                    if (inQuotes && next == '"')   // 转义引号 ""
                    {
                        current.Append('"');
                        i++;                       // 跳过下一个引号
                    }
                    else
                    {
                        inQuotes = !inQuotes;    // 进入/退出引号区
                    }
                }
                else if (ch == ',' && !inQuotes) // 分隔符（非引号内）
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }
            // 最后一列
            result.Add(current.ToString());
            return result;
        }
        //获取字符串的md5
        public static string GetMD5FromString(string buf)
        {
            MD5CryptoServiceProvider mD5CryptoServiceProvider = new MD5CryptoServiceProvider();
            byte[] value = mD5CryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(buf));
            return BitConverter.ToString(value).Replace("-", string.Empty);
        }

        //获取文件的md5
        public static string GetMd5HashFromFile(string fileName)
        {
            try
            {
                if (!File.Exists(fileName))
                    return "";
                FileStream file = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] retVal = md5.ComputeHash(file);
                file.Close();
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                {
                    sb.Append(retVal[i].ToString("x2"));
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                Debug.LogError("GetMd5HashFromFile fail,error: " + e.Message);
            }
            return "";
        }
        
        //截图
        public static Sprite CaptureTexture(RectTransform rt,string path,Vector2 size=default)
        {
            var rect = GetFrameRect(rt);
            var screenShot = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            screenShot.ReadPixels(rect, 0, 0, false);
            screenShot.Apply();

            if (size != default)
                screenShot = ScaleTexture(screenShot, (int)size.x, (int)size.y);
            var bytes = screenShot.EncodeToJPG();

            var dir = path.Remove(path.LastIndexOf('/'));
            if(!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, bytes);
            return Sprite.Create(screenShot, new Rect(0, 0, screenShot.width, screenShot.height), Vector2.one * 0.5f);
        }

        //获取相对原点rect
        private static Rect GetFrameRect(RectTransform rt)
        {
            var worldCorners = new Vector3[4];
            rt.GetWorldCorners(worldCorners);

            var bottomLeft = worldCorners[0];
            var topLeft = worldCorners[1];
            var topRight = worldCorners[2];
            var bottomRight = worldCorners[3];
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas == null)
                return rt.rect;

            switch (canvas.renderMode)
            {
                case RenderMode.ScreenSpaceOverlay:
                    break;
                case RenderMode.ScreenSpaceCamera:
                case RenderMode.WorldSpace:
                    var camera = canvas.worldCamera;
                    if (camera == null)
                        return rt.rect;
                    else
                    {
                        bottomLeft = camera.WorldToScreenPoint(bottomLeft);
                        topLeft = camera.WorldToScreenPoint(topLeft);
                        topRight = camera.WorldToScreenPoint(topRight);
                        bottomRight = camera.WorldToScreenPoint(bottomRight);
                    }
                    break;
            }

            float x = topLeft.x;
            //float y = Screen.height - topLeft.y; //左上原点
            float y = bottomLeft.y; //左下原点
            float width = topRight.x - topLeft.x;
            float height = topRight.y - bottomRight.y;
            return new Rect(x, y, width, height);          
        }

        // 图片压缩大小
        private static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            Texture2D result = new Texture2D(targetWidth, targetHeight, source.format, false);
            for (int i = 0; i < result.height; ++i)
            {
                for (int j = 0; j < result.width; ++j)
                {
                    Color newColor = source.GetPixelBilinear(j / (float)result.width, i / (float)result.height);
                    result.SetPixel(j, i, newColor);
                }
            }
            result.Apply();
            return result;
        }

        //字符串转图片
        public static Sprite Base64ToSprite(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);
            Texture2D tex2D = new Texture2D(100, 100);
            tex2D.LoadImage(bytes);
            Sprite s = Sprite.Create(tex2D, new Rect(0, 0, tex2D.width, tex2D.height), Vector2.one * 0.5f);
            return s;
        }
        
        //获取本机时区
        public static double GetZone()
        {
            DateTime utcTime = DateTime.Now.ToUniversalTime();
            TimeSpan ts = new TimeSpan(DateTime.Now.Ticks - utcTime.Ticks);
            return ts.TotalHours;
        }

        // 【秒级】获取时间（北京时间）
        public static DateTime GetDateTime(long timestamp)
        {
            long begtime = timestamp * 10000000;
            DateTime dt_1970 = new DateTime(1970, 1, 1, 8, 0, 0);
            long tricks_1970 = dt_1970.Ticks;//1970年1月1日刻度
            long time_tricks = tricks_1970 + begtime;//日志日期刻度
            DateTime dt = new DateTime(time_tricks);//转化为DateTime
            return dt;
        }

        // 【秒级】生成10位时间戳（北京时间）
        public static long GetTimeStamp(DateTime dt)
        {
            DateTime dateStart = new DateTime(1970, 1, 1, 8, 0, 0);
            return Convert.ToInt64((dt - dateStart).TotalSeconds);
        }

        /// <summary>
        /// 按【逗号且不在引号内】拆分一行，支持 "a,b" 嵌套逗号
        /// </summary>
        public static string[] SplitCSVLine(string line)
        {
            var list = new List<string>();
            var cur = new StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuote = !inQuote; // 切换引号状态
                }
                else if (c == ',' && !inQuote)
                {
                    list.Add(cur.ToString()); // 完整一列
                    cur.Clear();
                }
                else
                {
                    cur.Append(c);
                }
            }

            list.Add(cur.ToString()); // 最后一列
            return list.ToArray();
        }
        /// <summary>
        /// 按 RFC 4180 解析一行 CSV，特点：
        /// 1. 字段内可含逗号、换行、引号；
        /// 2. 成对 "" 转义为单个 "；
        /// 3. 引号边界符本身会保留在结果中，确保后续还原 JSON 等格式时无需再补引号。
        /// </summary>
        /// <param name="line">原始 CSV 行字符串</param>
        /// <returns>各字段列表，引号及内容原样保留</returns>
        public static string[] ParseCsvLineKeepQuotes(string line)
        {
            var list = new List<string>();
            var cur  = new System.Text.StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cur.Append('"');   // 转义成单个 "
                        i++;
                    }
                    else
                    {
                        inQuote = !inQuote;
                        cur.Append(c);     // ****** 关键：把引号也留下 ******
                    }
                }
                else if (c == ',' && !inQuote)
                {
                    list.Add(cur.ToString().Trim());
                    cur.Clear();
                }
                else
                {
                    cur.Append(c);
                }
            }
            list.Add(cur.ToString().Trim());
            return list.ToArray();
        }
    }
}
