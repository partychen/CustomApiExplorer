using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Web.Http.Routing;

namespace System.Collections.Generic
{
    /// <summary>
    /// Extension methods for <see cref="IDictionary{TKey,TValue}"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class DictionaryExtensions
    {
        /// <summary>
        /// Gets the value of <typeparamref name="T"/> associated with the specified key or <c>default</c> value if
        /// either the key is not present or the value is not of type <typeparamref name="T"/>. 
        /// </summary>
        /// <typeparam name="T">The type of the value associated with the specified key.</typeparam>
        /// <param name="collection">The <see cref="IDictionary{TKey,TValue}"/> instance where <c>TValue</c> is <c>object</c>.</param>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter.</param>
        /// <returns><c>true</c> if key was found, value is non-null, and value is of type <typeparamref name="T"/>; otherwise false.</returns>
        public static bool TryGetValue<T>(this IDictionary<string, object> collection, string key, out T value)
        {
            Contract.Assert(collection != null);

            object valueObj;
            if (collection.TryGetValue(key, out valueObj))
            {
                if (valueObj is T)
                {
                    value = (T)valueObj;
                    return true;
                }
            }

            value = default(T);
            return false;
        }

    }
}

namespace System.Web.Http.Routing
{
    public static class TypeHelper
    {
        internal static readonly Type ApiControllerType = typeof(ApiController);

        internal static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive ||
                   type.Equals(typeof(string)) ||
                   type.Equals(typeof(DateTime)) ||
                   type.Equals(typeof(Decimal)) ||
                   type.Equals(typeof(Guid)) ||
                   type.Equals(typeof(DateTimeOffset)) ||
                   type.Equals(typeof(TimeSpan));
        }

        internal static bool IsSimpleUnderlyingType(Type type)
        {
            Type underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                type = underlyingType;
            }

            return TypeHelper.IsSimpleType(type);
        }

        internal static bool CanConvertFromString(Type type)
        {
            return TypeHelper.IsSimpleUnderlyingType(type) ||
                TypeHelper.HasStringConverter(type);
        }

        internal static bool HasStringConverter(Type type)
        {
            return TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));
        }
    }

    internal static class RouteValueKeys
    {
        // Used to provide the action and controller name
        public const string Action = "action";
        public const string Controller = "controller";
    }
}

namespace System.Web.Http.Controllers
{
    internal static class HttpActionDescriptorExtensions
    {
        private const string AttributeRoutedPropertyKey = "MS_IsAttributeRouted";

        public static bool IsAttributeRouted(this HttpActionDescriptor actionDescriptor)
        {
            if (actionDescriptor == null)
            {
                throw new ArgumentNullException("actionDescriptor");
            }

            object value;
            actionDescriptor.Properties.TryGetValue(AttributeRoutedPropertyKey, out value);
            return value as bool? ?? false;
        }
    }

    internal static class HttpControllerDescriptorExtensions
    {
        private const string AttributeRoutedPropertyKey = "MS_IsAttributeRouted";

        public static bool IsAttributeRouted(this HttpControllerDescriptor controllerDescriptor)
        {
            if (controllerDescriptor == null)
            {
                throw new ArgumentNullException("controllerDescriptor");
            }

            object value;
            controllerDescriptor.Properties.TryGetValue(AttributeRoutedPropertyKey, out value);
            return value as bool? ?? false;
        }
    }

}

namespace System.Web.Http.Description
{
    public static class ApiParameterDescriptionExtension
    {
        public static IEnumerable<PropertyInfo> GetBindableProperties(this ApiParameterDescription apd)
        {
            return apd.ParameterDescriptor.ParameterType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetGetMethod() != null && p.GetSetMethod() != null);
        }

        public static bool CanConvertPropertiesFromString(this ApiParameterDescription apd)
        {
            return apd.GetBindableProperties().All(p => TypeHelper.CanConvertFromString(p.PropertyType));
        }

    }

}
