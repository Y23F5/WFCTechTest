using System;
using System.Linq;
using System.Reflection;

namespace WFCTechTest.WFC.Editor {
    /**
     * @file WfcEditorJsonBridge.cs
     * @brief Wraps optional JsonHelper reflection so editor windows do not duplicate discovery logic.
     */
    internal static class WfcEditorJsonBridge {
        private static Type _jsonHelperType;
        private static MethodInfo _serializeMethod;
        private static MethodInfo _deserializeMethod;
        private static bool _initialized;

        /**
         * @brief Tries to serialize the supplied payload with JsonHelper when available.
         */
        public static bool TrySerialize<T>(string path, T data) where T : class {
            if (!TryInitialize()) return false;

            try {
                var helper = Activator.CreateInstance(_jsonHelperType);
                _serializeMethod.MakeGenericMethod(typeof(T)).Invoke(helper, new object[] { path, data });
                return true;
            } catch {
                return false;
            }
        }

        /**
         * @brief Tries to deserialize the supplied path with JsonHelper when available.
         */
        public static bool TryDeserialize<T>(string path, out T data) where T : class {
            data = null;
            if (!TryInitialize()) return false;

            try {
                var helper = Activator.CreateInstance(_jsonHelperType);
                data = _deserializeMethod.MakeGenericMethod(typeof(T)).Invoke(helper, new object[] { path }) as T;
                return data != null;
            } catch {
                data = null;
                return false;
            }
        }

        private static bool TryInitialize() {
            if (_initialized) return _jsonHelperType != null && _serializeMethod != null && _deserializeMethod != null;

            _initialized = true;
            _jsonHelperType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => {
                    try {
                        return assembly.GetTypes();
                    } catch (ReflectionTypeLoadException exception) {
                        return exception.Types.Where(type => type != null);
                    }
                })
                .FirstOrDefault(type => type.Name == "JsonHelper");
            if (_jsonHelperType == null) return false;

            var publicInstanceMethods = _jsonHelperType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            _serializeMethod = publicInstanceMethods.FirstOrDefault(method => method.Name == "Serialize" && method.GetParameters().Length == 2);
            _deserializeMethod = publicInstanceMethods.FirstOrDefault(method => method.Name == "UnSerialize" && method.GetParameters().Length == 1);
            return _serializeMethod != null && _deserializeMethod != null;
        }
    }
}
