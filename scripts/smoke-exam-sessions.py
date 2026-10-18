#!/usr/bin/env python3
"""Exercise start/read API and Razor handlers against running services.
Creates guest test sessions in the configured local database; Identity is not required.
Usage: python3 scripts/smoke-exam-sessions.py [gateway_url] [web_url]
"""
import http.cookiejar
import json
import re
import sys
import uuid
import urllib.error
import urllib.parse
import urllib.request

GATEWAY = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080").rstrip("/")
WEB = (sys.argv[2] if len(sys.argv) > 2 else "http://localhost:5100").rstrip("/")


def request(url, payload=None, guest_key=None, opener=None, form=False):
    headers = {}
    if guest_key:
        headers["X-Exam-Guest-Key"] = guest_key
    data = None
    if payload is not None:
        data = urllib.parse.urlencode(payload).encode() if form else json.dumps(payload).encode()
        headers["Content-Type"] = "application/x-www-form-urlencoded" if form else "application/json"
    req = urllib.request.Request(url, data=data, headers=headers)
    try:
        with (opener or urllib.request.build_opener()).open(req, timeout=30) as response:
            return response.status, response.read().decode()
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()


def api(path, payload=None, guest_key=None, expected=200):
    status, body = request(GATEWAY + path, payload, guest_key)
    assert status == expected, f"{path}: expected {expected}, got {status}: {body[:300]}"
    return json.loads(body) if body else None


def csrf(html):
    match = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    assert match, "Antiforgery token is missing"
    return match.group(1)


def main():
    guest_key = uuid.uuid4().hex
    listed = api("/exam/api/exams")
    exam = next(e for e in listed["exams"]["items"] if e["questionCount"] > 0)
    exam_id = exam["id"]
    detail = api(f"/exam/api/exams/{exam_id}", guest_key=guest_key)
    part = detail["parts"][0]
    api(f"/exam/api/exams/{exam_id}/full-test", {}, expected=401)
    api(f"/exam/api/exams/{exam_id}/full-test", {}, "invalid-key", 401)
    full = api(f"/exam/api/exams/{exam_id}/full-test", {}, guest_key, 201)
    session = api(f'/exam/api/full-tests/{full["sessionId"]}', guest_key=guest_key)
    assert len(session["questions"]) == detail["totalQuestions"]
    assert session["expiresAt"].endswith("Z"), "Deadline must specify UTC"
    serialized = json.dumps(session).lower()
    assert "correctoption" not in serialized and "explanation" not in serialized
    selection = {"selectedPartNumbers": [part["partNumber"]], "selectedTags": [], "timeLimitMinutes": None}
    practice = api(f"/exam/api/exams/{exam_id}/practice", selection, guest_key, 201)
    practice_session = api(f'/exam/api/practice-sessions/{practice["sessionId"]}', guest_key=guest_key)
    assert practice_session["expiresAt"] is None
    assert len(practice_session["questions"]) == part["questionCount"]
    assert all(q["part"] == part["partNumber"] for q in practice_session["questions"])
    for invalid in ({"selectedPartNumbers": []}, {"selectedPartNumbers": [8]},
                    {"selectedPartNumbers": [1], "timeLimitMinutes": -1},
                    {"selectedPartNumbers": [1], "selectedTags": ["does-not-exist"]}):
        api(f"/exam/api/exams/{exam_id}/practice", invalid, guest_key, 400)
    other_key = uuid.uuid4().hex
    for route, sid in (("full-tests", full["sessionId"]), ("practice-sessions", practice["sessionId"])):
        api(f"/exam/api/{route}/{sid}", guest_key=other_key, expected=404)
    # Query-string userId cannot expose another learner's history.
    other_detail = api(f'/exam/api/exams/{exam_id}?userId={"guest:" + guest_key}', guest_key=other_key)
    assert other_detail["attemptHistory"] == []
    print("PASS: gateway API start/read, filters, UTC deadline, no answer leak, guest ownership, no Identity dependency", flush=True)

    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    status, html = request(WEB + f"/Exams/Detail/{exam_id}", opener=opener)
    assert status == 200 and "startPracticeSession()" in html
    antiforgery = csrf(html)
    for handler, payload, expected_count in (("StartFullTest", {}, detail["totalQuestions"]),
                                            ("StartPractice", selection, part["questionCount"])):
        req = urllib.request.Request(WEB + f"/Exams/Detail/{exam_id}?handler={handler}",
            data=json.dumps(payload).encode(), headers={"Content-Type": "application/json", "RequestVerificationToken": antiforgery})
        with opener.open(req) as response:
            assert response.status == 200
            redirect = json.load(response)["redirectUrl"]
        assert "/Account/Login" not in redirect
        status, page = request(WEB + redirect, opener=opener)
        assert status == 200
        question_data = re.search(r'questions: (\[.*\])', page)
        assert question_data and len(json.loads(question_data.group(1))) == expected_count
        assert 'id="exam-workspace"' in page and 'id="question-palette"' in page
        assert 'id="options-container"' in page and '_ExamTestAssets' not in page
        assert re.search(r'id="btn-submit"[^>]*disabled', page) and "<audio" not in page
        # A new browser session cannot open this browser's attempt.
        status, other_page = request(WEB + redirect)
        assert status == 200 and 'id="exam-workspace"' not in other_page
    # A null practice payload must not accidentally create a Full Test.
    invalid = urllib.request.Request(WEB + f"/Exams/Detail/{exam_id}?handler=StartPractice",
        data=b"null", headers={"Content-Type": "application/json", "RequestVerificationToken": antiforgery})
    try:
        opener.open(invalid)
        raise AssertionError("Null practice selection was accepted")
    except urllib.error.HTTPError as error:
        assert error.code == 400
    status, _ = request(WEB + f"/Exams/Detail/{exam_id}?handler=StartFullTest", {}, opener=opener)
    assert status == 400, "Start handler must reject requests without antiforgery token"
    print("PASS: anonymous Razor pages, antiforgery-protected start handlers and both question UIs", flush=True)


if __name__ == "__main__":
    main()
