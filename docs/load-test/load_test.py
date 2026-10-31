#!/usr/bin/env python3
"""Load test for a running demo instance. Usage: load_test.py URL EMAIL PASSWORD OUTPUT.json

Prepare a demo database first: every branch open, and plenty of stock for SKU-001 to SKU-010 at B001.
"""
import http.cookiejar, json, random, re, ssl, statistics, sys, threading, time, urllib.error, urllib.parse, urllib.request
from collections import Counter

URL, EMAIL, PASSWORD, OUTPUT = sys.argv[1:5]
CONTEXT = ssl._create_unverified_context()  # The local development certificate only.
TOKEN = re.compile(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"')


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None


class Client:
    def __init__(self):
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPSHandler(context=CONTEXT),
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),
            NoRedirect())
        self.login()

    def login(self):
        token = TOKEN.search(self.get("/Account/Login")).group(1)
        body = urllib.parse.urlencode({"Email": EMAIL, "Password": PASSWORD, "__RequestVerificationToken": token})
        request = urllib.request.Request(URL + "/Account/Login", body.encode())
        try:
            self.opener.open(request).read()
        except urllib.error.HTTPError as error:
            error.read()
        self.token = TOKEN.search(self.get("/Counter")).group(1)

    def get(self, path):
        return self.opener.open(URL + path).read().decode()

    def call(self, path, body=None):
        started = time.perf_counter()
        data = urllib.parse.urlencode(body).encode() if body else None
        try:
            with self.opener.open(urllib.request.Request(URL + path, data)) as response:
                response.read()
                status = response.status
        except urllib.error.HTTPError as error:
            error.read()
            status = error.code
        return status, (time.perf_counter() - started) * 1000

    def sale(self, sku):
        return self.call("/Counter/Create", {
            "CounterId": "C01",
            "RetailItems[0].ProductSku": sku,
            "RetailItems[0].Quantity": "1",
            "PaymentMethod": "Cash",
            "__RequestVerificationToken": self.token})


def summary(samples):
    ordered = sorted(samples)
    pick = lambda share: round(ordered[min(len(ordered) - 1, int(len(ordered) * share))], 1)
    return {"count": len(ordered), "p50": pick(.5), "p95": pick(.95), "p99": pick(.99), "max": round(ordered[-1], 1),
            "mean": round(statistics.mean(ordered), 1)}


def phase(name, workers, seconds, action):
    clients = [Client() for _ in range(workers)]
    results, lock, stop = [], threading.Lock(), time.time() + seconds

    def run(client):
        while time.time() < stop:
            kind, status, elapsed = action(client)
            with lock:
                results.append((kind, status, elapsed))

    threads = [threading.Thread(target=run, args=(client,)) for client in clients]
    started = time.time()
    [thread.start() for thread in threads]
    [thread.join() for thread in threads]
    elapsed = time.time() - started
    outcome = {"phase": name, "workers": workers, "seconds": round(elapsed), "requests_per_second": round(len(results) / elapsed, 1),
               "statuses": {f"{kind} {status}": count for (kind, status), count in sorted(Counter((k, s) for k, s, _ in results).items())},
               "latency_ms": {kind: summary([e for k, _, e in results if k == kind]) for kind in {k for k, _, _ in results}}}
    print(json.dumps(outcome), flush=True)
    return outcome


def contended(client):
    status, elapsed = client.sale("SKU-001")
    return "sale", status, elapsed


def mixed(client):
    roll = random.random()
    if roll < .5:
        status, elapsed = client.sale(f"SKU-{random.randint(2, 10):03d}")
        return "sale", status, elapsed
    path = "/Counter/Transactions" if roll < .75 else "/Catalog"
    status, elapsed = client.call(path)
    return "read", status, elapsed


results = [
    phase("baseline: 1 clerk, no contention", 1, 60, contended),
    phase("contention: 20 clerks, same product", 20, 60, contended),
    phase("sustained: 8 clerks, mixed work", 8, 300, mixed)]
json.dump(results, open(OUTPUT, "w"), indent=2)
