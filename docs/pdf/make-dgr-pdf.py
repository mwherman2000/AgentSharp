"""Build docs/DGR.pdf from docs/DGR.md.

Pages are portrait Letter. Wrap a part of the Markdown in

    <!-- pdf:landscape:start -->
    ...
    <!-- pdf:landscape:end -->

to print that part on landscape Letter pages (the comments are invisible on GitHub).
The picture is embedded from docs/images/dgr-bowls.svg, so regenerate that first
(python docs/images/make-dgr-svg.py) if you changed any tags.

Needs pandoc and Microsoft Edge. Usage: python docs/pdf/make-dgr-pdf.py [output.pdf]
"""
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
DOCS = os.path.normpath(os.path.join(HERE, ".."))
SOURCE = os.path.join(DOCS, "DGR.md")
CSS = os.path.join(HERE, "print.css")
TITLE = "Digitomic Genotype Recombination (DGR): recombining persona traits"


def find(name, candidates):
    found = shutil.which(name)
    if found:
        return found
    for path in candidates:
        if os.path.isfile(path):
            return path
    sys.exit(f"could not find {name}")


def main():
    out = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(DOCS, "DGR.pdf")
    pandoc = find("pandoc", [os.path.expandvars(r"%LOCALAPPDATA%\Pandoc\pandoc.exe")])
    edge = find("msedge", [r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                           r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"])

    with tempfile.TemporaryDirectory() as tmp:
        html_path = os.path.join(tmp, "dgr.html")
        subprocess.run(
            [pandoc, SOURCE, "-f", "gfm", "-t", "html5", "-s", "--embed-resources",
             "--metadata", f"pagetitle={TITLE}", "-c", CSS, "-o", html_path],
            cwd=DOCS, check=True)

        html = open(html_path, encoding="utf-8").read()
        start, end = html.count("<!-- pdf:landscape:start -->"), html.count("<!-- pdf:landscape:end -->")
        if start != end:
            sys.exit(f"unbalanced landscape markers: {start} start, {end} end")
        html = html.replace("<!-- pdf:landscape:start -->", '<div class="landscape">')
        html = html.replace("<!-- pdf:landscape:end -->", "</div>")
        open(html_path, "w", encoding="utf-8").write(html)

        subprocess.run(
            [edge, "--headless", "--disable-gpu", "--no-pdf-header-footer",
             f"--print-to-pdf={out}", "file:///" + html_path.replace("\\", "/")],
            check=True)
    print("wrote", out, f"({start} landscape section{'s' if start != 1 else ''})")


main()
