"""기획서 Markdown -> 인쇄용 HTML -> PDF 변환기.

사용법:
    python Docs/build_doc.py Docs/ShopPackageDesign.md Docs/ShopPackagePrint.css

동작:
    1. Markdown 을 HTML 로 변환한다 (표, 코드펜스 지원).
    2. CSS 와 이미지를 HTML 안에 인라인으로 박아넣는다. 결과 HTML 은 파일 하나로 완결된다.
    3. 헤드리스 Chrome 으로 같은 이름의 PDF 를 뽑는다.

이미지 표기:
    ![캡션](images/foo.png)             -> 본문 폭 전체
    ![캡션](images/foo.png){: .shot }   -> 세로 스크린샷 (폭 62mm)
    ![캡션](images/foo.png){: .half }   -> 본문 폭 절반
    캡션은 이미지 아래 설명글로 들어간다. 비워두면 설명글 없이 이미지만 들어간다.
"""

import base64
import mimetypes
import pathlib
import re
import subprocess
import sys
from urllib.parse import unquote

import markdown

CHROME_CANDIDATES = [
    r"C:/Program Files/Google/Chrome/Application/chrome.exe",
    r"C:/Program Files (x86)/Google/Chrome/Application/chrome.exe",
    r"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
]


def find_chrome():
    for path in CHROME_CANDIDATES:
        if pathlib.Path(path).exists():
            return path
    raise SystemExit("Chrome / Edge 를 찾지 못했습니다. CHROME_CANDIDATES 를 수정하세요.")


def inline_images(html, base_dir):
    """<img src="상대경로"> 를 data URI 로 치환한다."""

    def repl(match):
        src = match.group(1)
        if src.startswith(("data:", "http:", "https:")):
            return match.group(0)

        path = (base_dir / unquote(src)).resolve()
        if not path.exists():
            raise SystemExit(f"이미지를 찾지 못했습니다: {path}")

        mime = mimetypes.guess_type(path.name)[0] or "image/png"
        data = base64.b64encode(path.read_bytes()).decode("ascii")
        return f'src="data:{mime};base64,{data}"'

    return re.sub(r'src="([^"]+)"', repl, html)


def wrap_figures(html):
    """이미지 하나만 있는 문단을 figure 로 감싸고 alt 를 캡션으로 쓴다."""

    def repl(match):
        img = match.group(1)
        alt = re.search(r'alt="([^"]*)"', img)
        caption = alt.group(1).strip() if alt else ""
        body = f"<figure>{img}"
        if caption:
            body += f"<figcaption>{caption}</figcaption>"
        return body + "</figure>"

    return re.sub(r"<p>(<img [^>]*/?>)</p>", repl, html)


def build(md_path, css_path):
    md_path = pathlib.Path(md_path).resolve()
    css_path = pathlib.Path(css_path).resolve()
    base_dir = md_path.parent

    text = md_path.read_text(encoding="utf-8")
    title_match = re.search(r"^#\s+(.+)$", text, re.M)
    title = title_match.group(1).strip() if title_match else md_path.stem

    body = markdown.markdown(
        text,
        extensions=["tables", "fenced_code", "sane_lists", "attr_list"],
        output_format="html5",
    )
    body = wrap_figures(body)
    body = inline_images(body, base_dir)

    html = (
        "<!doctype html>\n"
        '<html lang="ko">\n<head>\n<meta charset="utf-8">\n'
        '<meta name="viewport" content="width=device-width, initial-scale=1">\n'
        f"<title>{title}</title>\n<style>\n"
        f"{css_path.read_text(encoding='utf-8')}\n"
        f"</style>\n</head>\n<body>\n{body}\n</body>\n</html>\n"
    )

    html_path = md_path.with_suffix(".html")
    html_path.write_text(html, encoding="utf-8")
    print(f"HTML  {html_path}")

    pdf_path = md_path.with_suffix(".pdf")
    subprocess.run(
        [
            find_chrome(),
            "--headless",
            "--disable-gpu",
            "--no-sandbox",
            "--no-pdf-header-footer",
            "--virtual-time-budget=15000",
            f"--print-to-pdf={pdf_path}",
            html_path.as_uri(),
        ],
        check=True,
        capture_output=True,
    )
    print(f"PDF   {pdf_path}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("사용법: python build_doc.py <문서.md> <인쇄용.css>")
    build(sys.argv[1], sys.argv[2])
