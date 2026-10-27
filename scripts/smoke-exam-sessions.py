#!/usr/bin/env python3
"""Exercise the complete exam lifecycle through the gateway and Razor handlers.
Creates guest sessions from published Content papers; each service has its own database.
Identity is not required.
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


def request(url, payload=None, guest_key=None, opener=None, form=False, method=None):
    headers = {}
    if guest_key:
        headers["X-Exam-Guest-Key"] = guest_key
    data = None
    if payload is not None:
        data = urllib.parse.urlencode(payload).encode() if form else json.dumps(payload).encode()
        headers["Content-Type"] = "application/x-www-form-urlencoded" if form else "application/json"
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with (opener or urllib.request.build_opener()).open(req, timeout=30) as response:
            return response.status, response.read().decode(errors="replace")
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()


def api(path, payload=None, guest_key=None, expected=200, method=None):
    status, body = request(GATEWAY + path, payload, guest_key, method=method)
    assert status == expected, f"{path}: expected {expected}, got {status}: {body[:300]}"
    return json.loads(body) if body else None


def csrf(html):
    match = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    assert match, "Antiforgery token is missing"
    return match.group(1)


def main():
    guest_key = uuid.uuid4().hex
    listed = api("/content/api/exams")
    exam = next(e for e in listed["exams"]["items"] if e["questionCount"] > 0)
    exam_id = exam["id"]
    detail = api(f"/content/api/exams/{exam_id}")
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
    api(f'/exam/api/exams/{exam_id}/attempts/me?userId={"guest:" + guest_key}', guest_key=other_key, expected=401)
    assert "attemptHistory" not in detail
    assert api(f"/exam/api/exams/{exam_id}/attempts/count") == detail["totalAttempts"] + 1
    first = session["questions"][0]
    answer = {str(first["id"]): first["options"][0]["letter"]}
    full_route = f'/exam/api/full-tests/{full["sessionId"]}'
    api(full_route + "/checkpoint", {"answers": answer}, guest_key, method="PUT")
    assert api(full_route, guest_key=guest_key)["answers"] == answer
    api(full_route + "/review", guest_key=guest_key, expected=404)
    result = api(full_route + "/submit", {"answers": answer}, guest_key)
    assert len(result["questions"]) == len(session["questions"])
    assert all("correctOption" in q for q in result["questions"])
    assert api(full_route + "/submit", {"answers": {}}, guest_key)["totalScore"] == result["totalScore"]
    api(full_route + "/checkpoint", {"answers": answer}, guest_key, expected=400, method="PUT")
    api(full_route + "/review", guest_key=other_key, expected=404)
    assert api("/exam/api/attempts/me", guest_key=guest_key)["attempts"]
    assert not api("/exam/api/attempts/me", guest_key=other_key)["attempts"]
    api(f"/content/internal/exam-papers/{exam_id}", expected=401)
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
        assert not re.search(r'id="btn-submit"[^>]*disabled', page)
        assert csrf(page)
        take_url = WEB + redirect
        route = take_url + ("&" if "?" in take_url else "?")
        def take_post(handler, body):
            req = urllib.request.Request(route + "handler=" + handler, data=json.dumps(body).encode(),
                headers={"Content-Type": "application/json", "RequestVerificationToken": csrf(page)})
            with opener.open(req) as response:
                return json.load(response)
        questions = json.loads(question_data.group(1))
        answer = {str(questions[0]["id"]): questions[0]["options"][0]["letter"]}
        assert take_post("Checkpoint", {"answers": answer})["saved"]
        result_url = take_post("Submit", {"answers": answer})["redirectUrl"]
        status, results_page = request(WEB + result_url, opener=opener)
        assert status == 200 and "Review answers" in results_page
        review_url = result_url.replace("/Results", "/Review")
        status, review_page = request(WEB + review_url, opener=opener)
        assert status == 200 and "examReviewConfig" in review_page
        # Subsequent visits to a submitted workspace redirect to its result.
        status, completed_page = request(take_url, opener=opener)
        assert status == 200 and "Review answers" in completed_page
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
    for path in ("/Performance/StudyHistory", "/Performance/ExamAttempts", "/Performance/IncorrectAnswers", "/js/exam-review.js", "/audio/sample-listening.wav"):
        assert request(WEB + path, opener=opener)[0] == 200
    print("PASS: gateway checkpoints, ownership, submit/retry, answer review, Razor results/history, CSRF and audio assets", flush=True)


if __name__ == "__main__":
    main()
