#!/usr/bin/env python3
"""Prepare a ClickDesk mirror inside an existing GitHub Pages checkout."""

import argparse
import re
import shutil
from pathlib import Path

SOURCE_ORIGIN = "https://clickdesk.still-moose-7651.chatgpt.site"
PAGES_ORIGIN = "https://caishoubin.github.io"
PUBLISHER = "ca-pub-6637568737373692"
SELLER = "google.com, pub-6637568737373692, DIRECT, f08c47fec0942fa0"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("destination", type=Path, help="Existing caishoubin.github.io checkout")
    root = parser.parse_args().destination.resolve()
    source = Path(__file__).resolve().parents[1] / "website"
    if not (root / ".git").is_dir() or not (root / "index.html").is_file():
        parser.error("Destination must contain the existing Pages repository and homepage")
    homepage = (root / "index.html").read_text(encoding="utf-8")
    publishers = re.findall(r'<meta\s+name=[\"\']google-adsense-account[\"\']\s+content=[\"\']([^\"\']+)', homepage)
    if publishers and publishers != [PUBLISHER]:
        parser.error("The existing homepage declares another publisher; review it before proceeding")
    menu = "<div class='list-h'>"
    if '<!-- ClickDesk project -->' not in homepage and menu not in homepage:
        parser.error("The blog navigation changed; add the ClickDesk project link manually")
    target = root / "clickdesk"
    if target.exists() and "ClickDesk" not in (target / "index.html").read_text(encoding="utf-8"):
        parser.error("The clickdesk directory already contains unrelated content")

    target.mkdir(exist_ok=True)
    for file in source.iterdir():
        if not file.is_file():
            continue
        if file.suffix not in {".html", ".xml", ".txt"}:
            shutil.copy2(file, target / file.name)
            continue
        content = file.read_text(encoding="utf-8").replace(SOURCE_ORIGIN, PAGES_ORIGIN + "/clickdesk")
        if file.suffix == ".html":
            content = re.sub(r'((?:href|src)=[\"\'])/(?!/)', r'\1/clickdesk/', content)
        if file.name == "privacy.html":
            start = content.index('<section aria-labelledby="hosting-data">')
            end = content.index("</section>", start) + len("</section>")
            content = content[:start] + (
                '<section aria-labelledby="hosting-data"><h2 id="hosting-data">Hosting and technical information</h2>'
                '<p>This version of the ClickDesk website is hosted with GitHub Pages. GitHub records visitors’ IP addresses for security purposes, whether or not visitors are signed in to GitHub. '
                'Its processing is described in <a href="https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement" target="_blank" rel="noopener noreferrer">GitHub’s Privacy Statement</a>.</p>'
                '<p>The website’s own code does not provide a sign-up form, analytics tracker, or recording upload service.</p></section>'
            ) + content[end:]
        (target / file.name).write_text(content, encoding="utf-8")

    if not publishers:
        homepage = homepage.replace("</head>", f'  <meta name="google-adsense-account" content="{PUBLISHER}">\n</head>', 1)
    if '<!-- ClickDesk project -->' not in homepage:
        homepage = homepage.replace(menu, menu + '\n        <!-- ClickDesk project -->\n        <a href="/clickdesk/" aria-label="ClickDesk Windows mouse recorder"><img src="/clickdesk/favicon.svg" alt=""><p>ClickDesk</p></a>', 1)
    (root / "index.html").write_text(homepage, encoding="utf-8")
    ads = root / "ads.txt"
    existing = ads.read_text(encoding="utf-8") if ads.exists() else ""
    if SELLER not in existing.splitlines():
        ads.write_text(existing.rstrip() + ("\n" if existing.strip() else "") + SELLER + "\n", encoding="utf-8")
    print(f"Prepared {PAGES_ORIGIN}/clickdesk/ with root publisher verification")


if __name__ == "__main__":
    main()
