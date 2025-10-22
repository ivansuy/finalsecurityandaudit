#!/usr/bin/env python3
"""Simulate login traffic against the AutoInventory backend.

This utility generates configurable waves of successful and failed login
attempts against the ``POST /api/auth/login`` endpoint. It is intended to help
exercise rate limiting, alerting and anomaly detection logic for the login
flow.

Examples
--------
Simulate 100 requests with a 30% success rate using credentials stored in two
CSV files, at a rate of 8 concurrent workers::

    python tools/login_traffic_simulator.py \
        --base-url http://localhost:5000 \
        --good-credentials data/good_creds.csv \
        --bad-credentials data/bad_creds.csv \
        --total-requests 100 --concurrency 8 --success-rate 0.3

"""
from __future__ import annotations

import argparse
import csv
import json
import math
import random
import sys
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, Iterator, List, Optional, Sequence

try:  # ``requests`` keeps the script lightweight while supporting HTTPS.
    import requests
except ImportError as exc:  # pragma: no cover - handled at runtime
    print(
        "The 'requests' package is required. Install the tooling requirements\n"
        "with: pip install -r tools/requirements.txt",
        file=sys.stderr,
    )
    raise SystemExit(1)


@dataclass(frozen=True)
class Credential:
    email: str
    password: str
    otp: Optional[str] = None


@dataclass(frozen=True)
class Attempt:
    credential: Credential
    expected_success: bool
    ip: str
    delay_before: float


@dataclass
class AttemptResult:
    attempt: Attempt
    status: Optional[int]
    ok: bool
    latency_s: float
    message: str
    requires_mfa: bool
    error: Optional[str]


_thread_local = threading.local()


def _get_session(verify_tls: bool) -> requests.Session:
    """Reuse a requests session per thread to avoid connection churn."""
    session = getattr(_thread_local, "session", None)
    if session is None:
        session = requests.Session()
        session.verify = verify_tls
        _thread_local.session = session
    return session


def parse_credential_sources(sources: Sequence[str]) -> List[Credential]:
    if not sources:
        raise ValueError("At least one credential source must be provided")

    entries: List[str] = []
    for src in sources:
        path = Path(src)
        if path.is_file():
            entries.extend(_read_credentials_from_file(path))
        else:
            entries.append(src)

    credentials = [_parse_credential_entry(entry) for entry in entries]
    if not credentials:
        raise ValueError("No credentials could be loaded from the given inputs")
    return credentials


def _read_credentials_from_file(path: Path) -> Iterable[str]:
    with path.open("r", encoding="utf-8") as handle:
        for line in handle:
            stripped = line.strip()
            if not stripped or stripped.startswith("#"):
                continue
            entries = [entry.strip() for entry in stripped.split(";") if entry.strip()]
            if entries:
                yield from entries


def _parse_credential_entry(entry: str) -> Credential:
    parts = [segment.strip() for segment in entry.split(",")]
    if len(parts) < 2:
        raise ValueError(
            f"Invalid credential entry '{entry}'. Expected the format 'email,password[,otp]'"
        )
    email, password = parts[0], parts[1]
    otp = parts[2] if len(parts) >= 3 and parts[2] else None
    return Credential(email=email, password=password, otp=otp)


def parse_ip_pool(values: Sequence[str], default_size: int) -> List[str]:
    ips: List[str] = []
    for raw in values:
        cleaned = raw.strip()
        if not cleaned:
            continue
        if cleaned.lower().startswith("random:"):
            try:
                amount = int(cleaned.split(":", 1)[1])
            except (IndexError, ValueError) as exc:
                raise ValueError(
                    f"Invalid random IP directive '{cleaned}'. Use 'random:<count>'."
                ) from exc
            ips.extend(generate_random_ips(amount))
        else:
            ips.append(cleaned)

    if not ips:
        ips = generate_random_ips(max(1, default_size))
    return ips


def generate_random_ips(amount: int) -> List[str]:
    return [".".join(str(random.randint(1, 254)) for _ in range(4)) for _ in range(amount)]


def build_attempts(
    *,
    total: int,
    success_rate: float,
    good_creds: Sequence[Credential],
    bad_creds: Sequence[Credential],
    ips: Sequence[str],
    jitter: float,
) -> Iterator[Attempt]:
    success_rate = max(0.0, min(1.0, success_rate))
    good_cycle = _cycle_list(good_creds)
    bad_cycle = _cycle_list(bad_creds)
    ip_cycle = _cycle_list(list(ips))

    for _ in range(total):
        expected_success = random.random() < success_rate
        credential = next(good_cycle) if expected_success else next(bad_cycle)
        delay = random.random() * jitter if jitter > 0 else 0.0
        yield Attempt(
            credential=credential,
            expected_success=expected_success,
            ip=next(ip_cycle),
            delay_before=delay,
        )


def _cycle_list(items: Sequence) -> Iterator:
    if not items:
        raise ValueError("Cannot build attempts without credential data")
    while True:
        for element in items:
            yield element


def perform_attempt(
    attempt: Attempt,
    *,
    url: str,
    timeout: float,
    verify_tls: bool,
    base_headers: Dict[str, str],
) -> AttemptResult:
    if attempt.delay_before > 0:
        time.sleep(attempt.delay_before)

    session = _get_session(verify_tls)
    payload = {
        "email": attempt.credential.email,
        "password": attempt.credential.password,
    }
    if attempt.credential.otp:
        payload["otpCode"] = attempt.credential.otp

    headers = dict(base_headers)
    headers["X-Forwarded-For"] = attempt.ip

    start = time.perf_counter()
    try:
        response = session.post(url, json=payload, headers=headers, timeout=timeout)
        latency = time.perf_counter() - start

        message = ""
        requires_mfa = False
        try:
            data = response.json()
            if isinstance(data, dict):
                message = str(data.get("message") or "")
                requires_mfa = bool(data.get("requiresMfa"))
            else:
                message = json.dumps(data)
        except ValueError:
            message = response.text.strip()

        return AttemptResult(
            attempt=attempt,
            status=response.status_code,
            ok=response.ok,
            latency_s=latency,
            message=message,
            requires_mfa=requires_mfa,
            error=None,
        )
    except Exception as exc:  # pragma: no cover - network failures vary
        latency = time.perf_counter() - start
        return AttemptResult(
            attempt=attempt,
            status=None,
            ok=False,
            latency_s=latency,
            message="",
            requires_mfa=False,
            error=str(exc),
        )


class AggregatedStats:
    def __init__(self) -> None:
        self.total = 0
        self.expected_successes = 0
        self.expected_failures = 0
        self.actual_successes = 0
        self.actual_failures = 0
        self.mfa_required = 0
        self.blocked = 0
        self.errors = 0
        self.latencies: List[float] = []

    def register(self, result: AttemptResult) -> None:
        self.total += 1
        if result.attempt.expected_success:
            self.expected_successes += 1
        else:
            self.expected_failures += 1

        if result.error:
            self.errors += 1
        else:
            if result.ok:
                self.actual_successes += 1
            else:
                self.actual_failures += 1

            if result.requires_mfa:
                self.mfa_required += 1

            if result.message and "bloqueado" in result.message.lower():
                self.blocked += 1

        if result.latency_s >= 0:
            self.latencies.append(result.latency_s)

    def render_summary(self) -> str:
        lines = [
            f"Total attempts:       {self.total}",
            f"Expected successes:   {self.expected_successes}",
            f"Expected failures:    {self.expected_failures}",
            f"Actual 2xx responses: {self.actual_successes}",
            f"Non-2xx responses:    {self.actual_failures}",
            f"MFA required:         {self.mfa_required}",
            f"Blocked responses:    {self.blocked}",
            f"Transport errors:     {self.errors}",
        ]
        if self.latencies:
            avg = sum(self.latencies) / len(self.latencies)
            p95 = percentile(self.latencies, 0.95)
            p99 = percentile(self.latencies, 0.99)
            lines.extend(
                [
                    f"Average latency:     {avg * 1000:.2f} ms",
                    f"p95 latency:         {p95 * 1000:.2f} ms",
                    f"p99 latency:         {p99 * 1000:.2f} ms",
                ]
            )
        return "\n".join(lines)


def percentile(samples: Sequence[float], fraction: float) -> float:
    if not samples:
        return 0.0
    fraction = max(0.0, min(1.0, fraction))
    data = sorted(samples)
    k = (len(data) - 1) * fraction
    f = math.floor(k)
    c = math.ceil(k)
    if f == c:
        return data[int(k)]
    return data[f] + (data[c] - data[f]) * (k - f)


def parse_headers(pairs: Sequence[str]) -> Dict[str, str]:
    headers: Dict[str, str] = {}
    for raw in pairs:
        if "=" not in raw:
            raise ValueError(f"Invalid header '{raw}'. Expected KEY=VALUE")
        key, value = raw.split("=", 1)
        headers[key.strip()] = value.strip()
    return headers


def write_log_row(writer: csv.writer, index: int, result: AttemptResult) -> None:
    writer.writerow(
        [
            index,
            result.attempt.expected_success,
            result.attempt.credential.email,
            result.attempt.ip,
            result.status if result.status is not None else "",
            f"{result.latency_s * 1000:.2f}",
            result.ok,
            result.requires_mfa,
            result.message,
            result.error or "",
        ]
    )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default="http://localhost:5000", help="Backend root URL")
    parser.add_argument(
        "--endpoint",
        default="/api/auth/login",
        help="Relative path to the login endpoint",
    )
    parser.add_argument("--total-requests", type=int, default=50, help="Number of login attempts to send")
    parser.add_argument("--concurrency", type=int, default=4, help="Number of worker threads")
    parser.add_argument(
        "--success-rate",
        type=float,
        default=0.5,
        help="Desired ratio of successful attempts (0.0 - 1.0)",
    )
    parser.add_argument(
        "--good-credentials",
        nargs="+",
        required=True,
        help="Inline entries or files with 'email,password[,otp]' for valid users",
    )
    parser.add_argument(
        "--bad-credentials",
        nargs="+",
        required=True,
        help="Inline entries or files with 'email,password[,otp]' for invalid attempts",
    )
    parser.add_argument(
        "--ip-pool",
        nargs="*",
        default=[],
        help=(
            "IP list to rotate. Accepts raw IPv4 values or 'random:<count>'. "
            "Defaults to generating random addresses."
        ),
    )
    parser.add_argument(
        "--timeout",
        type=float,
        default=5.0,
        help="HTTP timeout in seconds for each request",
    )
    parser.add_argument(
        "--jitter",
        type=float,
        default=0.0,
        help="Maximum random delay in seconds before each attempt",
    )
    parser.add_argument(
        "--header",
        action="append",
        default=[],
        help="Additional request headers in KEY=VALUE format",
    )
    parser.add_argument(
        "--insecure",
        action="store_true",
        help="Skip TLS verification (useful for self-signed certificates)",
    )
    parser.add_argument(
        "--log-file",
        help="Optional CSV file to append detailed attempt results",
    )
    parser.add_argument(
        "--report-every",
        type=int,
        default=10,
        help="Emit progress every N completed attempts (0 to disable)",
    )
    return parser


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    try:
        good_creds = parse_credential_sources(args.good_credentials)
        bad_creds = parse_credential_sources(args.bad_credentials)
        ip_pool = parse_ip_pool(args.ip_pool, args.total_requests)
        headers = parse_headers(args.header)
    except ValueError as exc:
        parser.error(str(exc))
        return 2

    url = args.base_url.rstrip("/") + "/" + args.endpoint.lstrip("/")
    stats = AggregatedStats()
    attempts = list(
        build_attempts(
            total=args.total_requests,
            success_rate=args.success_rate,
            good_creds=good_creds,
            bad_creds=bad_creds,
            ips=ip_pool,
            jitter=args.jitter,
        )
    )

    log_writer: Optional[csv.writer] = None
    log_file_handle = None
    if args.log_file:
        log_path = Path(args.log_file)
        log_file_handle = log_path.open("a", newline="", encoding="utf-8")
        log_writer = csv.writer(log_file_handle)
        if log_file_handle.tell() == 0:
            log_writer.writerow(
                [
                    "index",
                    "expected_success",
                    "email",
                    "ip",
                    "status",
                    "latency_ms",
                    "http_ok",
                    "requires_mfa",
                    "message",
                    "error",
                ]
            )

    print(
        f"Dispatching {len(attempts)} login attempts to {url} with {args.concurrency} workers..."
    )

    from concurrent.futures import ThreadPoolExecutor, as_completed

    with ThreadPoolExecutor(max_workers=args.concurrency) as executor:
        future_to_index = {
            executor.submit(
                perform_attempt,
                attempt,
                url=url,
                timeout=args.timeout,
                verify_tls=not args.insecure,
                base_headers=headers,
            ): idx
            for idx, attempt in enumerate(attempts, start=1)
        }

        for future in as_completed(future_to_index):
            idx = future_to_index[future]
            try:
                result = future.result()
            except Exception as exc:  # pragma: no cover - defensive guard
                print(f"Attempt {idx} raised an unexpected error: {exc}", file=sys.stderr)
                continue

            stats.register(result)
            if log_writer is not None:
                write_log_row(log_writer, idx, result)

            if args.report_every and idx % args.report_every == 0:
                print(
                    f"Completed {idx}/{len(attempts)} attempts - "
                    f"{stats.actual_successes} success responses, "
                    f"{stats.actual_failures} failures, "
                    f"{stats.errors} transport errors"
                )

    if log_file_handle is not None:
        log_file_handle.close()

    print("\n=== Simulation summary ===")
    print(stats.render_summary())
    return 0


if __name__ == "__main__":  # pragma: no cover - CLI entry point
    sys.exit(main())
