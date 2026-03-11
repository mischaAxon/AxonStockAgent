#!/usr/bin/env bash
set -euo pipefail

# ─── Config ───────────────────────────────────────────────────────────────────
API_BASE="http://localhost/api/v1"
HEALTH_URL="http://localhost/health"
TIMEOUT=120  # max seconds to wait for API
TEST_EMAIL="smoketest@axon.local"
TEST_PASS="SmokeTest123!"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

PASS=0
FAIL=0

check() {
  local name="$1"
  local result="$2"
  local status="$3"

  if [ "$status" -eq 0 ]; then
    echo -e "  ${GREEN}✓${NC} $name"
    PASS=$((PASS + 1))
  else
    echo -e "  ${RED}✗${NC} $name"
    echo -e "    ${RED}Response: $result${NC}"
    FAIL=$((FAIL + 1))
  fi
}

# ─── Wait for API ─────────────────────────────────────────────────────────────
echo -e "${YELLOW}Waiting for API to be ready...${NC}"
elapsed=0
while ! curl -sf "$HEALTH_URL" > /dev/null 2>&1; do
  sleep 2
  elapsed=$((elapsed + 2))
  if [ "$elapsed" -ge "$TIMEOUT" ]; then
    echo -e "${RED}Timeout: API not ready after ${TIMEOUT}s${NC}"
    echo "Tip: run 'docker compose up -d' first, then run this script"
    exit 1
  fi
done
echo -e "${GREEN}API is ready (${elapsed}s)${NC}\n"

# ─── 1. Health check ──────────────────────────────────────────────────────────
echo "1. Health check"
HEALTH=$(curl -sf "$HEALTH_URL" 2>&1) && check "GET /health" "$HEALTH" 0 || check "GET /health" "$HEALTH" 1

# ─── 2. Auth ──────────────────────────────────────────────────────────────────
echo -e "\n2. Auth"
# Register — ignore failure if user already exists
REG_RESULT=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_BASE/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$TEST_EMAIL\",\"password\":\"$TEST_PASS\"}")
if [ "$REG_RESULT" = "200" ] || [ "$REG_RESULT" = "201" ] || [ "$REG_RESULT" = "409" ]; then
  check "POST /auth/register (HTTP $REG_RESULT)" "" 0
else
  check "POST /auth/register (HTTP $REG_RESULT)" "Unexpected status" 1
fi

# Login
LOGIN_RESULT=$(curl -sf -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$TEST_EMAIL\",\"password\":\"$TEST_PASS\"}" 2>&1)

TOKEN=$(echo "$LOGIN_RESULT" | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)

if [ -z "$TOKEN" ]; then
  echo -e "  ${RED}✗ Could not get access token. Stopping.${NC}"
  echo -e "  Response: $LOGIN_RESULT"
  exit 1
fi
check "POST /auth/login → got token" "" 0

AUTH="Authorization: Bearer $TOKEN"

# ─── 3. Dashboard ─────────────────────────────────────────────────────────────
echo -e "\n3. Dashboard"
DASH=$(curl -sf -H "$AUTH" "$API_BASE/dashboard" 2>&1) && check "GET /dashboard" "" 0 || check "GET /dashboard" "$DASH" 1

# ─── 4. Watchlist ─────────────────────────────────────────────────────────────
echo -e "\n4. Watchlist"
for SYM in AAPL MSFT ASML.AS; do
  ADD_RESULT=$(curl -sf -X POST -H "$AUTH" -H "Content-Type: application/json" \
    "$API_BASE/watchlist" -d "{\"symbol\":\"$SYM\"}" 2>&1) \
    && check "POST /watchlist ($SYM)" "" 0 \
    || check "POST /watchlist ($SYM)" "$ADD_RESULT" 1
done

WL=$(curl -sf -H "$AUTH" "$API_BASE/watchlist" 2>&1) && check "GET /watchlist" "" 0 || check "GET /watchlist" "$WL" 1

# ─── 5. Signals ───────────────────────────────────────────────────────────────
echo -e "\n5. Signals"
SIG=$(curl -sf -H "$AUTH" "$API_BASE/signals?page=1&limit=5" 2>&1) \
  && check "GET /signals?page=1&limit=5" "" 0 \
  || check "GET /signals?page=1&limit=5" "$SIG" 1

SIG_LATEST=$(curl -sf -H "$AUTH" "$API_BASE/signals/latest" 2>&1) \
  && check "GET /signals/latest" "" 0 \
  || check "GET /signals/latest" "$SIG_LATEST" 1

SIG_STATS=$(curl -sf -H "$AUTH" "$API_BASE/signals/stats" 2>&1) \
  && check "GET /signals/stats" "" 0 \
  || check "GET /signals/stats" "$SIG_STATS" 1

# Since filter — cross-platform date (Linux & macOS)
SINCE_DATE=$(date -u -d '7 days ago' +%Y-%m-%dT%H:%M:%SZ 2>/dev/null \
  || date -u -v-7d +%Y-%m-%dT%H:%M:%SZ)
SIG_SINCE=$(curl -sf -H "$AUTH" "$API_BASE/signals?since=$SINCE_DATE" 2>&1) \
  && check "GET /signals?since=7d_ago" "" 0 \
  || check "GET /signals?since=7d_ago" "$SIG_SINCE" 1

# ─── 6. Portfolio ─────────────────────────────────────────────────────────────
echo -e "\n6. Portfolio"
PORT_ADD=$(curl -sf -X POST -H "$AUTH" -H "Content-Type: application/json" \
  "$API_BASE/portfolio" -d '{"symbol":"AAPL","shares":10,"avgBuyPrice":175.50}' 2>&1) \
  && check "POST /portfolio (AAPL)" "" 0 \
  || check "POST /portfolio (AAPL)" "$PORT_ADD" 1

PORT=$(curl -sf -H "$AUTH" "$API_BASE/portfolio" 2>&1) \
  && check "GET /portfolio" "" 0 \
  || check "GET /portfolio" "$PORT" 1

# ─── 7. Sectors & News ────────────────────────────────────────────────────────
echo -e "\n7. Sectors & News"
SEC=$(curl -sf -H "$AUTH" "$API_BASE/sectors" 2>&1) \
  && check "GET /sectors" "" 0 \
  || check "GET /sectors" "$SEC" 1

NEWS=$(curl -sf -H "$AUTH" "$API_BASE/news/latest" 2>&1) \
  && check "GET /news/latest" "" 0 \
  || check "GET /news/latest" "$NEWS" 1

SENT=$(curl -sf -H "$AUTH" "$API_BASE/news/sector-sentiment" 2>&1) \
  && check "GET /news/sector-sentiment" "" 0 \
  || check "GET /news/sector-sentiment" "$SENT" 1

TREND=$(curl -sf -H "$AUTH" "$API_BASE/news/trending" 2>&1) \
  && check "GET /news/trending" "" 0 \
  || check "GET /news/trending" "$TREND" 1

# ─── 8. Fundamentals ──────────────────────────────────────────────────────────
echo -e "\n8. Fundamentals"
FUND=$(curl -sf -H "$AUTH" "$API_BASE/fundamentals/AAPL" 2>&1) \
  && check "GET /fundamentals/AAPL" "" 0 \
  || check "GET /fundamentals/AAPL" "$FUND" 1

# ─── Summary ──────────────────────────────────────────────────────────────────
echo -e "\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
TOTAL=$((PASS + FAIL))
echo -e "  ${GREEN}Passed: $PASS${NC} / $TOTAL"
if [ "$FAIL" -gt 0 ]; then
  echo -e "  ${RED}Failed: $FAIL${NC} / $TOTAL"
else
  echo -e "  ${GREEN}All checks passed!${NC}"
fi
echo -e "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

echo -e "\n${YELLOW}Note:${NC} Test user ($TEST_EMAIL) and data persist in the DB."
echo "      To clean up: docker compose down -v  (removes all data)"

exit $FAIL
