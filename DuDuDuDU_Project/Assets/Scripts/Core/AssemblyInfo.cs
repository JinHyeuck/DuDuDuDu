using System.Runtime.CompilerServices;

// 테스트가 internal 을 볼 수 있게 한다.
//
// 지금 필요한 것은 SaveFile.ReplaceByMove 하나다. 그건 File.Replace 를 못 쓰는 플랫폼
// (안드로이드·iOS 실기)에서만 타는 경로라 Windows 에디터에서는 절대 실행되지 않는다.
// public 으로 열면 호출부가 실수로 원자적이지 않은 쪽을 쓸 수 있고, 그렇다고 닫아 두면
// <b>확인할 수 없는 코드</b>가 된다. internal + 테스트가 그 사이의 답이다.
//
// 이 통로를 "테스트하기 귀찮아서" 넓히지 말 것. 순수 함수는 전부 public 이라 필요가 없다.
[assembly: InternalsVisibleTo("OJ.Core.Tests")]
