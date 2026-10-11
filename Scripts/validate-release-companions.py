"""Validate release documents and the current authenticated purchase links."""

from html.parser import HTMLParser
from pathlib import Path
import re
import sys


class PricingCards(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.cards = []
        self.current = None
        self.in_heading = False

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "article":
            self.current = {"heading": "", "text": "", "buttons": []}
        if self.current is not None:
            if tag == "h2":
                self.in_heading = True
            if tag == "a" and "button" in attrs.get("class", "").split():
                self.current["buttons"].append(attrs.get("href", ""))

    def handle_data(self, data):
        if self.current is not None:
            self.current["text"] += data
            if self.in_heading:
                self.current["heading"] += data

    def handle_endtag(self, tag):
        if tag == "h2":
            self.in_heading = False
        if tag == "article" and self.current is not None:
            self.cards.append(self.current)
            self.current = None


def validate(root):
    errors = []
    documents = {}
    required = [
        "Docs/BENCHMARK_PROTOCOL.md",
        "Docs/EDGE_CASES_AND_NEGATIVE_TESTS.md",
        "Docs/RELEASE_EVIDENCE.md",
        "Web/wwwroot/landing.html",
        "Web/wwwroot/pricing.html",
        "Web/wwwroot/account.html",
    ]
    for name in required:
        try:
            text = (root / name).read_text(encoding="utf-8-sig")
            if not text.strip():
                raise ValueError("missing or empty release document")
            documents[name] = text
            print(f"PASS: non-empty {name}")
        except (OSError, UnicodeError, ValueError) as error:
            errors.append((name, str(error)))

    matrix = "Docs/EDGE_CASES_AND_NEGATIVE_TESTS.md"
    if matrix in documents and "Counter-tests" not in documents[matrix]:
        errors.append((matrix, "Counter-tests section is missing"))

    pricing = "Web/wwwroot/pricing.html"
    if pricing in documents:
        parser = PricingCards()
        parser.feed(documents[pricing])
        for title, price in [("Pay-as-you-go", "19.99"), ("Unlimited", "49.99")]:
            cards = [card for card in parser.cards if card["heading"].strip().startswith(title)]
            if len(cards) != 1:
                errors.append((pricing, f"Expected one {title} pricing card"))
                continue
            card = cards[0]
            if not re.search(r"€\s*" + re.escape(price) + r"(?![0-9])", card["text"]):
                errors.append((pricing, f"{title} must show the approved €{price} price"))
            if card["buttons"] != ["/account"]:
                errors.append((pricing, f"{title} subscription must open /account for authenticated checkout"))

    for name, message in errors:
        print(f"::error file={name}::{message}")
    if not errors:
        print("PASS: release companions, pricing and authenticated purchase links")
    return 1 if errors else 0


if __name__ == "__main__":
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]
    sys.exit(validate(root))
