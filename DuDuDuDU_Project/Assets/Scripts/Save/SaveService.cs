using System;
using System.Collections.Generic;
using OJ.Core;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.DI;

namespace OJ.Save
{
    /// <summary>
    /// 통합 세이브 파일의 소유자. (MIGRATION_BASELINE 8.7)
    ///
    /// <b>왜 소유자가 필요한가.</b> 지금은 매니저 9개가 각자 PlayerPrefs 키에 따로 저장한다.
    /// 통합 파일로 옮기는 순간 <b>저장의 단위가 매니저에서 파일로 바뀐다</b> — 누가
    /// 저장하든 파일 전체를 써야 하고, 그 안에는 아직 살아 있지 않은 매니저의 몫도 들어 있다.
    /// 그대로 쓰면 그 몫이 기본값으로 덮인다. 그래서 <see cref="SaveState"/> 하나를
    /// 메모리에 두고 매니저들이 자기 몫만 고치는 구조가 필요하다. 그것이 이 클래스다.
    ///
    /// <b>쓰기를 먼저 켜고, 검증한 뒤에 읽기를 켰다.</b> 그 사이 기간에는 파일에 쓰기만 하고
    /// 게임은 PlayerPrefs 로 돌았다 — 매핑이 틀렸어도 <b>아무도 그 파일을 읽지 않으니</b>
    /// 진행도가 손상될 수 없는 상태였다. F10 대조로 파일 내용이 실제 상태와 같다는 것을
    /// 확인하고 나서 읽기를 켰고(8.7), 그 뒤 <b>7.5 에서 PlayerPrefs 경로를 지웠다.</b>
    ///
    /// <b>이제 이 파일이 유일한 진행도다.</b> 되돌아갈 자리가 없어졌다는 뜻이라,
    /// 8.7 때는 PlayerPrefs 가 대신 해 주던 방어를 이 클래스가 직접 해야 한다.
    /// 그것이 <see cref="WriteBlocked"/> 다 — <b>읽지 못했으면 쓰지도 않는다.</b>
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class SaveService : ISaveOnApplicationLifecycle
    {
        private readonly IReadOnlyList<ISaveStateOwner> owners;
        private readonly string path;

        private bool writeBlocked;
        private bool blockReported;
        private bool saving;

        public SaveService(IReadOnlyList<ISaveStateOwner> owners)
        {
            this.owners = owners;
            path = SavePaths.SaveFilePath;
        }

        /// <summary>
        /// 세이브를 <b>읽지 못했으므로 쓰지도 않는</b> 상태인가.
        ///
        /// <b>왜 필요한가.</b> 8.7 까지는 파일이 깨져도 게임이 PlayerPrefs 로 복원된 진짜
        /// 상태로 돌았고, 종료할 때 그 정상값이 깨진 파일을 덮었다 — <b>자가치유</b>였다.
        /// 7.5 로 그 경로를 지운 뒤에는 같은 상황에서 게임이 <b>기본값</b>으로 돌고, 종료할 때
        /// 그 기본값이 본 파일과 백업을 <b>둘 다</b> 덮는다. 자가치유가 자가파괴로 뒤집힌다.
        ///
        /// 디스크 오류나 권한 문제처럼 <b>고칠 수 있었던</b> 손상이 그 순간 영구 소실로
        /// 확정되는 것이라, 읽기에 실패한 세션에서는 아예 쓰지 않는다.
        ///
        /// <b>파일이 없는 것(<see cref="SaveSource.None"/>)은 여기 해당하지 않는다.</b>
        /// 그건 첫 실행이고, 그때 쓰지 않으면 세이브가 영영 만들어지지 않는다.
        /// </summary>
        public bool WriteBlocked => writeBlocked;

        /// <summary>지금 메모리 상태를 모아 만든 <see cref="SaveState"/>.</summary>
        public SaveState Capture()
        {
            var state = new SaveState();
            for (int i = 0; i < owners.Count; i++)
            {
                try
                {
                    owners[i].WriteTo(state);
                }
                catch (Exception ex)
                {
                    // 하나가 실패해도 나머지는 담아야 한다. 다만 <b>그 상태로 파일을 쓰면
                    // 안 된다</b> — 빠진 몫이 기본값으로 굳는다. 그래서 Save 쪽에서
                    // 예외 발생 여부를 보고 쓰기를 취소한다.
                    Debug.LogError("[저장] " + owners[i].GetType().Name + " WriteTo 실패: " + ex);
                    throw;
                }
            }

            return state;
        }

        /// <summary>
        /// 파일에 쓴다. <see cref="Capture"/> 가 실패하면 <b>쓰지 않는다</b> —
        /// 일부만 담긴 상태를 파일에 굳히면 그 매니저의 진행도가 사라진다.
        /// </summary>
        public void SaveAll()
        {
            if (writeBlocked)
            {
                // 처음 한 번만 크게 알린다. 거래마다 저장되므로 매번 찍으면 로그가
                // 읽을 수 없게 되고, 정작 다른 사고를 가린다. 상태 자체는
                // <see cref="WriteBlocked"/> 로 남아 F9 자체 진단이 계속 보여 준다.
                if (!blockReported)
                {
                    blockReported = true;
                    Debug.LogError(
                        "[저장] 이 세션은 세이브를 읽지 못했으므로 쓰지 않는다. " +
                        "지금 쓰면 기본값이 본 파일과 백업을 둘 다 덮어 복구가 불가능해진다. " +
                        "이후 저장 요청은 조용히 무시한다 (F9 로 상태 확인).");
                }

                return;
            }

            // 재진입 방지. 지금은 WriteTo 안에서 저장을 부르는 곳이 없지만, 생기면
            // 무한 재귀로 스택이 터지는 형태로 나타난다 — 원인을 찾기 아주 나쁜 사고다.
            if (saving)
                return;

            saving = true;
            try
            {
                SaveState state;
                try
                {
                    state = Capture();
                }
                catch
                {
                    Debug.LogError("[저장] 상태를 모으지 못해 파일 쓰기를 건너뛴다. 기존 세이브는 그대로다.");
                    return;
                }

                try
                {
                    SaveFile.Save(path, state);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[저장] 파일 쓰기 실패: " + ex);
                }
            }
            finally
            {
                saving = false;
            }
        }

        /// <summary>
        /// 파일에서 읽어 매니저들에게 나눠 준다.
        ///
        /// 읽을 수 없는 세이브(<see cref="SaveSource.Unreadable"/>)면 <b>아무것도 하지 않고
        /// 그 세션의 쓰기를 막는다</b>(<see cref="WriteBlocked"/>). 읽기만 건너뛰는 것으로는
        /// 부족하다 — 기본값으로 돌던 게임이 종료할 때 그 기본값을 파일에 굳혀 버린다.
        /// </summary>
        public bool TryLoadAll()
        {
            SaveLoadResult result = SaveFile.Load(path);

            if (result.Source == SaveSource.Unreadable)
            {
                writeBlocked = true;
                Debug.LogError("[저장] 세이브를 읽을 수 없다. 이 세션은 저장하지 않는다. " + result.Message);
                return false;
            }

            // 파일이 없는 것은 사고가 아니라 첫 실행이다. 여기서 쓰기를 막으면
            // 세이브가 영영 만들어지지 않는다.
            if (!result.IsUsable)
                return false;

            if (!string.IsNullOrEmpty(result.Message))
                Debug.LogWarning("[저장] " + result.Message);

            for (int i = 0; i < owners.Count; i++)
            {
                try
                {
                    owners[i].ReadFrom(result.State);
                }
                catch (Exception ex)
                {
                    // 여기서 던지면 뒤쪽 매니저가 통째로 로드되지 않는다.
                    // 한 매니저가 기본값으로 시작하는 것이 전부가 죽는 것보다 낫다.
                    Debug.LogError("[저장] " + owners[i].GetType().Name + " ReadFrom 실패: " + ex);
                }
            }

            VerifyRoundTrip(result.State);
            return true;
        }

        /// <summary>
        /// 방금 읽어 들인 것을 <b>다시 모아서</b> 원본과 대조한다.
        ///
        /// <b>왜 필요한가.</b> F10 대조는 <c>WriteTo</c> 만 증명한다. <c>ReadFrom</c> 은
        /// 실제로 불러 봐야 알 수 있는데, 그건 살아 있는 상태를 덮는 행위라 미리 시험할 수 없다.
        /// 그래서 진짜 로드가 일어나는 이 순간에 확인한다.
        ///
        /// <b>왜 JSON 문자열 비교인가.</b> <c>SaveState</c> 는 <c>SortedDictionary</c> 라
        /// 직렬화 결과가 넣은 순서와 무관하게 항상 같다(테스트로 잠가 뒀다). 그래서
        /// 문자열이 같다는 것은 곧 <b>모든 필드가 같다</b>는 뜻이고, 필드를 하나씩 비교하는
        /// 코드를 따로 만들 필요가 없다. 새 필드가 늘어도 자동으로 검사 대상이 된다.
        ///
        /// 어긋나면 <c>ReadFrom</c> 이 무언가를 빠뜨렸거나 다르게 해석한 것이다. 그대로 두면
        /// <b>다음 저장에서 그 손실이 파일과 PlayerPrefs 양쪽에 굳는다.</b> 그래서 조용히
        /// 넘기지 않는다.
        /// </summary>
        private void VerifyRoundTrip(SaveState loaded)
        {
            string before;
            string after;
            try
            {
                before = SaveSerializer.Serialize(loaded);
                after = SaveSerializer.Serialize(Capture());
            }
            catch (Exception ex)
            {
                Debug.LogError("[저장] 로드 후 대조에 실패했다: " + ex);
                return;
            }

            if (before == after)
                return;

            Debug.LogError(
                "[저장] 로드한 내용과 로드 후 상태가 다르다. ReadFrom 이 무언가를 빠뜨렸다." +
                Environment.NewLine + FirstDifference(before, after));
        }

        /// <summary>
        /// 어디서 갈렸는지 한 조각만 보여 준다. 3KB 짜리 JSON 두 개를 통째로 로그에 쏟으면
        /// 정작 다른 지점을 찾기가 더 어렵다.
        /// </summary>
        private static string FirstDifference(string a, string b)
        {
            int n = Math.Min(a.Length, b.Length);
            int i = 0;
            while (i < n && a[i] == b[i])
                i++;

            int from = Math.Max(0, i - 80);
            return "  파일 : ..." + Slice(a, from, i + 80) + Environment.NewLine +
                   "  메모리: ..." + Slice(b, from, i + 80);
        }

        private static string Slice(string s, int from, int to)
        {
            to = Math.Min(to, s.Length);
            return from >= to ? "(끝)" : s.Substring(from, to - from).Replace("\r", string.Empty).Replace("\n", " ");
        }
    }
}
