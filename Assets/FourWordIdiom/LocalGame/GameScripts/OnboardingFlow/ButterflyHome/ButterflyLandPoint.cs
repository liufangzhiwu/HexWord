using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum LandArea
{
    TOP,
    BOTTOM,
}
public class ButterflyLandPoint : MonoBehaviour
{
    [SerializeField] public LandArea area;
    /// <summary>
    /// 是否被占用
    /// </summary>
    [NonSerialized] public bool Occupied = false;  // 是否被占用
    /// <summary>
    /// 占用者
    /// </summary>
    [NonSerialized] public Transform OccupiedBy = null;  // 占用者
    /// <summary>
    /// 所属物体
    /// </summary>
    public Transform OwnerObject => transform.parent;    // 所属物体 （挂到物体子点）
}
