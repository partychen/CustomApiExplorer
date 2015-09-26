using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.CustomApiExplorer
{
    public static class MethodHelper
    {
        public static MethodInfo GetExtensionMethod(Type type, string methodName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (assembly.FullName.Contains("System.Web.Http"))
                {
                    foreach (var t in assembly.GetTypes())
                    {
                        foreach (
                            var method in
                                t.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (method.Name == methodName && method.GetParameters()[0].ParameterType == type)
                            {
                                return method;
                            }
                        }
                    }
                }
            }
            return null;
        }

        public static MethodInfo GetStaticMethod(string typeFullName, string methodName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (assembly.FullName.Contains("System.Web.Http"))
                {
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.FullName == typeFullName)
                        {
                            foreach (
                                var method in
                                    t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                            {
                                if (method.Name == methodName)
                                {
                                    return method;
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        public static object InvokePrivateMethod(object o, string methodName, object[] parameters = null, Type type = null)
        {
            var belongToType = type ?? o.GetType();
            var method = belongToType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (method == null)
            {
                throw new Exception("Cannot find method");
            }
            return method.Invoke(o, parameters ?? new object[] { });
        }

        public static object InvokeExtensionMethod(object o, string methodName, object[] parameters = null, Type type = null)
        {
            var method = GetExtensionMethod(type ?? o.GetType(), methodName);
            if (method == null)
            {
                throw new Exception("Cannot find method");
            }
            var invokeParameters = new List<object> {o};
            invokeParameters.AddRange(parameters ?? new object[] { });
            return method.Invoke(o, invokeParameters.ToArray());
        }

        public static object InvokeStaticMethod(string typeFullName, string methodName, object[] parameters = null)
        {
            var method = GetStaticMethod(typeFullName, methodName);
            if (method == null)
            {
                throw new Exception("Cannot find method");
            }
            return method.Invoke(null, parameters ?? new object[] { });
        }

    }

    public static class ObjectExtension
    {
        public static object CallPrivateMethod(this object o, string methodName, object[] parameters = null, Type type = null)
        {
            return MethodHelper.InvokePrivateMethod(o, methodName, parameters, type);
        }

        public static object CallExtensionMethod(this object o, string methodName, object[] parameters = null, Type type = null)
        {
            return MethodHelper.InvokeExtensionMethod(o, methodName, parameters, type);
        }

        public static object CallBasePrivateMethod(this object o, string methodName, object[] parameters = null)
        {
            return MethodHelper.InvokePrivateMethod(o, methodName, parameters, o.GetType().BaseType);
        }
    }
}