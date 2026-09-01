using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OJ.Core
{
    /// <summary>세이브 텍스트가 <see cref="SaveState"/> 로 읽히지 않을 때 던진다.</summary>
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message) { }
        public SaveFormatException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// <see cref="SaveState"/> ↔ JSON. (MIGRATION_BASELINE 7.2)
    ///
    /// <b>왜 <c>JsonUtility</c> 가 아닌가.</b> 세 가지가 걸린다.
    /// <list type="number">
    /// <item><c>JsonUtility</c> 는 엔진 API 라 <b>에디터 밖에서 못 돈다.</b> 7단계 게이트가
    /// "저장·로드 왕복 후 모든 값이 동일"인데, 그걸 헤드리스로 검증할 수 없게 된다.</item>
    /// <item><c>Dictionary</c> 를 못 다룬다. 리스트로 우회하면 키 중복을 막을 수 없다.</item>
    /// <item><b>모양이 틀려도 조용히 기본값을 준다.</b> 잘린 파일을 먹으면 예외 대신
    /// "전부 0 인 세이브"가 나오고, 그게 그대로 덮어 쓰이면 진행도가 사라진다.</item>
    /// </list>
    ///
    /// <b>Newtonsoft 타입은 이 파일 밖으로 새지 않는다.</b> 호출부는 <see cref="SaveFormatException"/>
    /// 만 알면 된다. 나중에 직렬화기를 바꾸더라도 이 파일만 바뀐다.
    /// </summary>
    public static class SaveSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            // 파일이 작고(수 KB) 사람이 열어 볼 일이 있다. 들여쓰기 값어치가 있다.
            Formatting = Formatting.Indented,

            // JSON 관례대로 camelCase 로 내보낸다. DTO 에 특성을 붙이지 않기 위해
            // 여기 설정으로만 처리한다 — SaveState 는 평범한 POCO 로 남는다.
            //
            // ProcessDictionaryKeys = false 가 핵심이다. Newtonsoft 가 흔히 쓰이는
            // CamelCasePropertyNamesContractResolver 는 이 값이 true 라서 <b>딕셔너리 키까지</b>
            // 소문자로 바꾼다. 여기서는 키가 enum 이름이므로 "Gold" 가 "gold" 로 저장되고,
            // 대소문자만 다른 두 키는 아예 하나로 합쳐져 값 하나가 조용히 사라진다.
            // 실제로 처음 이 설정으로 짰다가 테스트 8건이 그것을 잡아냈다.
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = false,
                    OverrideSpecifiedNames = true,
                },
            },

            // 모르는 키는 무시한다. 새 빌드에서 필드를 지웠을 때 옛 세이브가 못 읽히면 안 된다.
            MissingMemberHandling = MissingMemberHandling.Ignore,

            // 기본값이면 Newtonsoft 가 날짜처럼 보이는 문자열을 DateTime 으로 바꿔 버린다.
            // 보석 id·보상 id 는 임의 문자열이라 그런 해석이 끼면 왕복이 깨진다.
            DateParseHandling = DateParseHandling.None,

            // get-only 컬렉션을 기존 인스턴스에 채워 넣게 한다. SaveState 가 비교자를
            // Ordinal 로 고정해 둔 것이 이 설정 덕분에 살아남는다.
            ObjectCreationHandling = ObjectCreationHandling.Auto,
        };

        public static string Serialize(SaveState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return JsonConvert.SerializeObject(state, Settings);
        }

        /// <summary>
        /// JSON 을 읽는다. 읽을 수 없으면 <see cref="SaveFormatException"/> 을 던진다 —
        /// <b>빈 <see cref="SaveState"/> 를 돌려주지 않는다.</b> 그렇게 하면 호출부가
        /// "세이브가 없다"와 "세이브가 깨졌다"를 구별할 수 없고, 깨진 쪽을 새 파일로
        /// 덮어써서 복구 가능한 백업까지 날리게 된다.
        /// </summary>
        public static SaveState Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new SaveFormatException("세이브 내용이 비어 있다.");

            SaveState state;
            try
            {
                state = JsonConvert.DeserializeObject<SaveState>(json, Settings);
            }
            catch (JsonException ex)
            {
                throw new SaveFormatException("세이브 JSON 을 읽을 수 없다: " + ex.Message, ex);
            }

            // "null" 은 문법상 올바른 JSON 이라 위에서 예외가 안 난다. 여기서 걸러야 한다.
            if (state == null)
                throw new SaveFormatException("세이브 JSON 이 null 이다.");

            return state;
        }
    }
}
