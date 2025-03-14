﻿using System.Reflection;
using Jotunn.Utils;
using UnityEngine;

namespace Jotunn {
    internal class PropertyMember : MemberBase
    {
        private readonly PropertyInfo propertyInfo;

        public PropertyMember(PropertyInfo propertyInfo)
        {
            this.propertyInfo = propertyInfo;
            MemberType = propertyInfo.PropertyType;
            IsUnityObject = MemberType.IsSameOrSubclass(typeof(Object));
            IsClass = MemberType.IsClass;
            HasGetMethod = propertyInfo.GetIndexParameters().Length == 0 && propertyInfo.GetMethod != null;
            EnumeratedType = MemberType.GetEnumeratedType();
            IsEnumerableOfUnityObjects = EnumeratedType?.IsSameOrSubclass(typeof(Object)) == true;
            IsEnumeratedClass = EnumeratedType?.IsClass == true;
        }

        public override object GetValue(object obj)
        {
            return propertyInfo.GetValue(obj);
        }

        public override void SetValue(object obj, object value)
        {
            propertyInfo.SetValue(obj, value);
        }
    }
}
