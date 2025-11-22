using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Middleware
{
    /// <summary>
    /// 自定义特性,用于组件类的属性
    /// 标记属性为空的时候自动查找子组件并赋值
    /// <see cref="InitEmptyProperty"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class AutoAssign : Attribute
    {
        /// <summary>
        /// 注入规则：
        /// （1）按照组件名称自动注入，对象属性且私有属性
        /// （2）变量的名称必须和对象的名称一致，大小写必须一致
        /// </summary>
        public static void AutoInject(MonoBehaviour that)
        {
            var type = that.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            Dictionary<string, FieldInfo> field_infos = new Dictionary<string, FieldInfo>();

            foreach (var field in fields)
            {
                // 遍历字段,如果字段标记了该特性,并且为空值,则加入字典
                var attr = field.GetCustomAttribute<AutoAssign>();
                if (attr == null) continue;
                object value = field.GetValue(that);
                if (value != null && !value.Equals(null))
                    continue;

                field_infos.Add(field.Name, field);
            }

            // 遍历所有子组件,如果字典中存在对应的属性，则赋值
            foreach (var node in that.transform.GetComponentsInChildren<Transform>())
            {
                var name = node.name;
                if (field_infos.TryGetValue(name, out var field))
                {
                    var com = node.GetComponent(field.FieldType);
                    if (com != null)
                        field.SetValue(that, com);
                }
            }
        }
    }
}