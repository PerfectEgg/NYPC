#include <iostream>
#include <vector>
#include <string>
#include <sstream>
#include <queue>
#include <algorithm>
#include <set>

using namespace std;

// 사각형 구조체
struct Rect {
    int r1, c1, r2, c2;
    int score;

    bool operator<(const Rect& other) const {
        return score < other.score; // 우선순위 큐에서 점수 높은 것이 top
    }
};

class Game {
private:
    vector<vector<int>> board;       // 게임 보드
    bool first;                      // 선공 여부
    bool passed;                     // 패스 여부
    priority_queue<Rect> rectPQ;     // 후보 사각형 우선순위 큐
    set<string> rectSet;             // 중복 제거용

    string rectKey(int r1, int c1, int r2, int c2) {
        return to_string(r1) + "," + to_string(c1) + "," + to_string(r2) + "," + to_string(c2);
    }

    // 사각형 유효성 검사
    bool isValid(int r1, int c1, int r2, int c2) {
        int sum = 0;
        bool edgeFilled = false;

        for (int r = r1; r <= r2; ++r) {
            for (int c = c1; c <= c2; ++c) {
                int val = board[r][c];
                if (val > 0) sum += val;
                if (val != 0 && (r == r1 || r == r2 || c == c1 || c == c2)) edgeFilled = true;
                if (val < 0) return false; // 내/상대 땅 겹치면 무효
            }
        }

        return sum == 10 && edgeFilled;
    }

    // 후보 큐에 추가 (중복 제거)
    void pushRect(int r1, int c1, int r2, int c2) {
        string key = rectKey(r1, c1, r2, c2);
        if (rectSet.count(key)) return;
        rectSet.insert(key);
        rectPQ.push({r1, c1, r2, c2, (r2 - r1 + 1) * (c2 - c1 + 1)});
    }

public:
    Game() {}

    Game(const vector<vector<int>>& b, bool f) : board(b), first(f), passed(false) {
        initializeAllRects();
    }

    // 보드 전체 전수 조사
    void initializeAllRects() {
        int n = board.size(), m = board[0].size();
        for (int r1 = 0; r1 < n; ++r1) {
            for (int c1 = 0; c1 < m; ++c1) {
                for (int r2 = r1; r2 < n; ++r2) {
                    for (int c2 = c1; c2 < m; ++c2) {
                        if (isValid(r1, c1, r2, c2))
                            pushRect(r1, c1, r2, c2);
                    }
                }
            }
        }
    }

    // 선택 사각형과 겹치는 후보 제거
    void invalidateRectsByMove(int r1, int c1, int r2, int c2) {
        priority_queue<Rect> newPQ;
        set<string> newSet;

        while (!rectPQ.empty()) {
            Rect r = rectPQ.top(); rectPQ.pop();
            if (r.r2 < r1 || r.r1 > r2 || r.c2 < c1 || r.c1 > c2) {
                string key = rectKey(r.r1, r.c1, r.r2, r.c2);
                if (!newSet.count(key)) {
                    newPQ.push(r);
                    newSet.insert(key);
                }
            }
        }
        rectPQ = newPQ;
        rectSet = newSet;
    }

    // 상대 후보 최대 점수 시뮬레이션
    int simulateOpponentMaxScore(const Rect& myMove) {
        priority_queue<Rect> tempPQ = rectPQ;
        int maxScore = 0;

        while (!tempPQ.empty()) {
            Rect r = tempPQ.top(); tempPQ.pop();

            // 내 선택 사각형과 겹치면 무효
            if (!(r.r2 < myMove.r1 || r.r1 > myMove.r2 || r.c2 < myMove.c1 || r.c1 > myMove.c2)) continue;

            // 실제 보드 검사: -1/-2 포함 시 무효
            bool invalid = false;
            for (int rr = r.r1; rr <= r.r2 && !invalid; ++rr)
                for (int cc = r.c1; cc <= r.c2; ++cc)
                    if (board[rr][cc] < 0) invalid = true;

            if (invalid) continue;

            maxScore = max(maxScore, r.score);
        }
        return maxScore;
    }

    // 내 턴 계산
    vector<int> calculateMove(int myTime, int oppTime) {
        if (rectPQ.empty()) return {-1, -1, -1, -1};

        vector<Rect> candidates;
        priority_queue<Rect> tempPQ = rectPQ;

        int cnt = 0;
        while (!tempPQ.empty() && cnt < 10) {
            candidates.push_back(tempPQ.top()); tempPQ.pop();
            cnt++;
        }

        Rect best = candidates[0];
        int bestNetGain = -99999;

        for (auto& myMove : candidates) {
            int oppGain = simulateOpponentMaxScore(myMove);
            int netGain = myMove.score - oppGain;
            if (netGain > bestNetGain) {
                bestNetGain = netGain;
                best = myMove;
            }
        }

        return {best.r1, best.c1, best.r2, best.c2};
    }

    // 선택/상대 땅 반영
    void updateMove(int r1, int c1, int r2, int c2, bool isMyMove) {
        if (r1 == -1) { passed = true; return; }

        int marker = isMyMove ? -1 : -2;
        for (int r = r1; r <= r2; ++r)
            for (int c = c1; c <= c2; ++c)
                board[r][c] = marker;

        invalidateRectsByMove(r1, c1, r2, c2);
        passed = false;
    }

    // 상대 턴 처리 + 주변 후보 추가
    void updateOpponentAction(const vector<int>& action, int time) {
        int r1 = action[0], c1 = action[1], r2 = action[2], c2 = action[3];
        updateMove(r1, c1, r2, c2, false);

        int n = board.size(), m = board[0].size();
        const int MAX_EXPAND = 5;
        int sr = max(0, r1 - MAX_EXPAND), er = min(n - 1, r2 + MAX_EXPAND);
        int sc = max(0, c1 - MAX_EXPAND), ec = min(m - 1, c2 + MAX_EXPAND);

        for (int nr1 = sr; nr1 <= er; ++nr1) {
            for (int nc1 = sc; nc1 <= ec; ++nc1) {
                for (int nr2 = nr1; nr2 <= er; ++nr2) {
                    for (int nc2 = nc1; nc2 <= ec; ++nc2) {
                        if (!isValid(nr1, nc1, nr2, nc2)) continue;
                        pushRect(nr1, nc1, nr2, nc2);
                    }
                }
            }
        }
    }
};

// 메인
int main() {
    Game game;
    bool first = false;

    while (true) {
        string line;
        getline(cin, line);
        istringstream iss(line);
        string command;
        if (!(iss >> command)) continue;

        if (command == "READY") {
            string turn; iss >> turn;
            first = (turn == "FIRST");
            cout << "OK" << endl;
        } else if (command == "INIT") {
            vector<vector<int>> board;
            string row;
            while (iss >> row) {
                vector<int> boardRow;
                for (char c : row) boardRow.push_back(c - '0');
                board.push_back(boardRow);
            }
            game = Game(board, first);
        } else if (command == "TIME") {
            int myTime, oppTime; iss >> myTime >> oppTime;
            vector<int> move = game.calculateMove(myTime, oppTime);
            game.updateMove(move[0], move[1], move[2], move[3], true);
            cout << move[0] << " " << move[1] << " " << move[2] << " " << move[3] << endl;
        } else if (command == "OPP") {
            int r1, c1, r2, c2, time; iss >> r1 >> c1 >> r2 >> c2 >> time;
            game.updateOpponentAction({r1, c1, r2, c2}, time);
        } else if (command == "FINISH") break;
    }
    return 0;
}
