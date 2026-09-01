using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;

namespace OJ.Headless
{
    /// <summary>
    /// 테스트 어셈블리를 <b>진짜 NUnit</b> 으로 돌린다.
    ///
    /// [TestFixture]/[Test]/[TestCase]/[TestCaseSource] 의 해석도, Assert 의 평가도 전부
    /// nunit.framework.dll 이 한다. 여기서 하는 일은 리플렉션으로 NUnit 의 공개 실행 API
    /// (NUnitTestAssemblyRunner + DefaultTestAssemblyBuilder)를 호출하고, 결과를 NUnit3 결과
    /// XML 로 받아 오는 것뿐이다. 단언 로직을 흉내 낸 코드는 이 파일에 한 줄도 없다.
    ///
    /// nunit 을 컴파일 타임 참조가 아니라 리플렉션으로 잡는 이유: PackageCache 경로에 해시가 붙어
    /// 버전마다 달라지기 때문이다. 경로를 박아 두면 패키지가 갱신될 때마다 도구가 깨진다.
    /// </summary>
    internal sealed class NUnitHost
    {
        private readonly Assembly nunit;

        private NUnitHost(Assembly nunit)
        {
            this.nunit = nunit;
        }

        public string NUnitVersion
        {
            get { return nunit.GetName().Version.ToString(); }
        }

        /// <summary>
        /// 런타임 어셈블리 해석기를 걸고 nunit 을 로드한다.
        /// 테스트 어셈블리는 OJ.Core / nunit.framework / UnityEngine.* 를 참조하는데, 이 프로세스의
        /// 기본 탐색 경로에는 셋 다 없다. 그래서 직접 찾아 준다.
        /// </summary>
        public static NUnitHost Create(string nunitPath, IEnumerable<string> probeDirectories)
        {
            var directories = probeDirectories
                .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                foreach (string directory in directories)
                {
                    string candidate = Path.Combine(directory, name.Name + ".dll");
                    if (File.Exists(candidate))
                        return context.LoadFromAssemblyPath(candidate);
                }

                return null;
            };

            return new NUnitHost(Assembly.LoadFrom(nunitPath));
        }

        public Assembly LoadTestAssembly(string path)
        {
            return Assembly.LoadFrom(Path.GetFullPath(path));
        }

        /// <summary>NUnit 을 돌리고 결과를 NUnit3 결과 XML 로 돌려준다.</summary>
        public XElement Run(Assembly testAssembly, string filterRegex)
        {
            Type builderType = Require("NUnit.Framework.Api.DefaultTestAssemblyBuilder");
            Type runnerType = Require("NUnit.Framework.Api.NUnitTestAssemblyRunner");

            object builder = Activator.CreateInstance(builderType);
            object runner = Activator.CreateInstance(runnerType, new object[] { builder });

            MethodInfo load = runnerType.GetMethod("Load", new[] { typeof(Assembly), typeof(IDictionary<string, object>) });
            if (load == null)
                throw new InvalidOperationException("NUnitTestAssemblyRunner.Load(Assembly, IDictionary) 를 못 찾았다.");

            load.Invoke(runner, new object[] { testAssembly, new Dictionary<string, object>() });

            object listener = CreateNullListener();
            object filter = CreateFilter(filterRegex);

            MethodInfo run = runnerType.GetMethods()
                .FirstOrDefault(m => m.Name == "Run" && m.GetParameters().Length == 2);
            if (run == null)
                throw new InvalidOperationException("NUnitTestAssemblyRunner.Run(ITestListener, ITestFilter) 를 못 찾았다.");

            object result = run.Invoke(runner, new[] { listener, filter });
            if (result == null)
                throw new InvalidOperationException("NUnit 이 결과를 돌려주지 않았다.");

            MethodInfo toXml = result.GetType().GetMethod("ToXml", new[] { typeof(bool) });
            if (toXml == null)
                throw new InvalidOperationException("ITestResult.ToXml(bool) 을 못 찾았다.");

            object node = toXml.Invoke(result, new object[] { true });
            PropertyInfo outerXml = node.GetType().GetProperty("OuterXml");
            if (outerXml == null)
                throw new InvalidOperationException("TNode.OuterXml 을 못 찾았다.");

            return XElement.Parse((string)outerXml.GetValue(node));
        }

        private object CreateNullListener()
        {
            Type listenerType = Require("NUnit.Framework.Internal.TestListener");

            PropertyInfo property = listenerType.GetProperty("NULL", BindingFlags.Public | BindingFlags.Static);
            if (property != null)
                return property.GetValue(null);

            FieldInfo field = listenerType.GetField("NULL", BindingFlags.Public | BindingFlags.Static);
            if (field != null)
                return field.GetValue(null);

            throw new InvalidOperationException("TestListener.NULL 을 못 찾았다.");
        }

        /// <summary>
        /// --filter 는 NUnit 자체 필터로 넘긴다. 러너가 임의로 테스트를 걸러 내면
        /// "0건 통과"를 통과로 착각하기 쉬워서, 거르는 일도 NUnit 에 맡긴다.
        /// </summary>
        private object CreateFilter(string filterRegex)
        {
            Type filterType = Require("NUnit.Framework.Internal.TestFilter");

            if (string.IsNullOrEmpty(filterRegex))
            {
                PropertyInfo emptyProperty = filterType.GetProperty("Empty", BindingFlags.Public | BindingFlags.Static);
                if (emptyProperty != null)
                    return emptyProperty.GetValue(null);

                FieldInfo emptyField = filterType.GetField("Empty", BindingFlags.Public | BindingFlags.Static);
                if (emptyField != null)
                    return emptyField.GetValue(null);

                throw new InvalidOperationException("TestFilter.Empty 를 못 찾았다.");
            }

            MethodInfo fromXml = filterType.GetMethod("FromXml", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(string) }, null);
            if (fromXml == null)
            {
                throw new InvalidOperationException(
                    "이 NUnit 빌드에는 TestFilter.FromXml 이 없어 --filter 를 쓸 수 없다.");
            }

            string xml = "<filter><test re='1'>" + Escape(filterRegex) + "</test></filter>";
            return fromXml.Invoke(null, new object[] { xml });
        }

        private static string Escape(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private Type Require(string fullName)
        {
            Type type = nunit.GetType(fullName, false);
            if (type == null)
                throw new InvalidOperationException("nunit.framework.dll 에 " + fullName + " 이 없다.");

            return type;
        }

        public static string Attribute(XElement element, string name, string fallback)
        {
            XAttribute attribute = element.Attribute(name);
            return attribute == null ? fallback : attribute.Value;
        }

        public static double DoubleAttribute(XElement element, string name)
        {
            double value;
            return double.TryParse(Attribute(element, name, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : 0d;
        }
    }
}
