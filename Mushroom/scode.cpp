#include <iostream>
#include <vector>
#include <string>
#include <sstream>
#include <algorithm>

using namespace std;

struct Rect {
    int r1, c1, r2, c2;
    Rect(int _r1, int _c1, int _r2, int _c2)
        : r1(_r1), c1(_c1), r2(_r2), c2(_c2) {}
    bool operator<(const Rect& other) const {
        if (r1 != other.r1) return r1 < other.r1;
        if (c1 != other.c1) return c1 < other.c1;
        if (r2 != other.r2) return r2 < other.r2;
        return c2 < other.c2;
    }
    vector<int> toVector() const {
        return {r1, c1, r2, c2};
    }
};

class Game {
private:
    vector<vector<int>> board;
    int rows, cols;

    vector<Rect> candidateRects;

public:
    Game() : rows(0), cols(0) {}

    Game(const vector<vector<int>>& b)
        : board(b), rows((int)b.size()), cols((int)b[0].size()) {}

    // 직사각형 합 계산
    int sumInRect(const Rect& rect) {
        int s = 0;
        for (int r = rect.r1; r <= rect.r2; ++r)
            for (int c = rect.c1; c <= rect.c2; ++c)
                s += board[r][c];
        return s;
    }

    // 네 변 조건 검사
    bool isValid(int r1, int c1, int r2, int c2) {
        bool r1fit = false, r2fit = false, c1fit = false, c2fit = false;
        int sums = 0;
        for (int r = r1; r <= r2; ++r) {
            for (int c = c1; c <= c2; ++c) {
                if (board[r][c] != 0) {
                    sums += board[r][c];
                    if (r == r1) r1fit = true;
                    if (r == r2) r2fit = true;
                    if (c == c1) c1fit = true;
                    if (c == c2) c2fit = true;
                }
            }
        }
        return sums == 10 && r1fit && r2fit && c1fit && c2fit;
    }

    // 후보 사각형 전수 조사
    void generateCandidateRects() {
        candidateRects.clear();
        for (int r1 = 0; r1 < rows; ++r1) {
            for (int c1 = 0; c1 < cols; ++c1) {
                for (int r2 = r1; r2 < rows; ++r2) {
                    for (int c2 = c1; c2 < cols; ++c2) {
                        Rect rect(r1, c1, r2, c2);
                        if (sumInRect(rect) == 10 && isValid(r1, c1, r2, c2)) {
                            candidateRects.push_back(rect);
                        }
                    }
                }
            }
        }
    }

    // 점수 계산
    int evaluateGain(const Rect& rect, const vector<vector<int>>& b) {
        int gain = 0;
        for (int r = rect.r1; r <= rect.r2; ++r)
            for (int c = rect.c1; c <= rect.c2; ++c) {
                if (b[r][c] == 0) continue;
                else if (b[r][c] == 9) continue; // 내 칸
                else gain += 1;
            }
        return gain;
    }

    // 상대가 반격할 수 있는 최대 점수 예측
    int simulateOpponentMove(const Rect& myMove) {
        vector<vector<int>> newBoard = board;
        for (int r = myMove.r1; r <= myMove.r2; ++r)
            for (int c = myMove.c1; c <= myMove.c2; ++c)
                if (newBoard[r][c] != 0)
                    newBoard[r][c] = 9; // 내 점령 표시

        int maxLoss = 0;
        for (const Rect& rect : candidateRects) {
            int s = 0;
            bool valid = true;
            for (int r = rect.r1; r <= rect.r2 && valid; ++r) {
                for (int c = rect.c1; c <= rect.c2; ++c) {
                    if (newBoard[r][c] == 0) valid = false;
                    else if (newBoard[r][c] == 9) s += 2; // 내 칸 빼앗길 위험
                    else s += 1;
                }
            }
            if (valid) maxLoss = max(maxLoss, s);
        }
        return maxLoss;
    }

    // 후보 중 상위 K개 점수 높은 것 추출
    vector<Rect> getTopKCandidates(int K) {
        vector<pair<int, Rect>> scored;
        for (const auto& rect : candidateRects) {
            int score = evaluateGain(rect, board);
            scored.push_back({score, rect});
        }
        sort(scored.begin(), scored.end(), [](auto& a, auto& b) {
            return a.first > b.first;
        });
        vector<Rect> top;
        for (int i = 0; i < (int)scored.size() && i < K; ++i) {
            top.push_back(scored[i].second);
        }
        return top;
    }

    // 겹침 검사
    bool isOverlapping(const Rect& a, const Rect& b) {
        return !(a.r2 < b.r1 || b.r2 < a.r1 || a.c2 < b.c1 || b.c2 < a.c1);
    }

    // 후보 사각형 갱신 (점령한 영역과 겹치는 후보 제거 + 새 후보 추가)
    void updateCandidateRects(const Rect& myMove) {
        vector<Rect> updated;
        for (const Rect& rect : candidateRects) {
            if (!isOverlapping(rect, myMove))
                updated.push_back(rect);
        }
        candidateRects = updated;

        // 주변 작은 사각형 후보 재탐색 (간단히 주변 3x3 영역)
        for (int r = myMove.r1; r <= myMove.r2; ++r) {
            for (int c = myMove.c1; c <= myMove.c2; ++c) {
                for (int h = 1; h <= 3; ++h) {
                    for (int w = 1; w <= 3; ++w) {
                        int nr = r + h - 1;
                        int nc = c + w - 1;
                        if (nr < rows && nc < cols) {
                            Rect candidate(r, c, nr, nc);
                            if (sumInRect(candidate) == 10 && isValid(candidate.r1, candidate.c1, candidate.r2, candidate.c2)) {
                                candidateRects.push_back(candidate);
                            }
                        }
                    }
                }
            }
        }
    }

    // 최종 수 계산 함수
    vector<int> calculateMove(int myTime, int oppTime) {
        vector<Rect> topMoves = getTopKCandidates(5);
        if (topMoves.empty()) return {-1, -1, -1, -1};

        int bestScore = -1e9;
        Rect bestMove(-1, -1, -1, -1);

        for (auto& rect : topMoves) {
            int myGain = evaluateGain(rect, board);
            int oppLoss = simulateOpponentMove(rect);
            int netGain = myGain - oppLoss;
            if (netGain > bestScore) {
                bestScore = netGain;
                bestMove = rect;
            }
        }
        if (bestMove.r1 == -1) return {-1, -1, -1, -1};

        // 실제 보드에 적용 및 후보 갱신
        for (int r = bestMove.r1; r <= bestMove.r2; ++r)
            for (int c = bestMove.c1; c <= bestMove.c2; ++c)
                board[r][c] = 0;

        updateCandidateRects(bestMove);

        return bestMove.toVector();
    }

    // 상대 수 업데이트 (보드 반영)
    void updateOpponentAction(const vector<int>& action) {
        if (action[0] == -1) return;
        for (int r = action[0]; r <= action[2]; ++r)
            for (int c = action[1]; c <= action[3]; ++c)
                board[r][c] = 0;
        // 상대 수 반영 후 후보 갱신
        updateCandidateRects(Rect(action[0], action[1], action[2], action[3]));
    }
};

// --- main ---

int main() {
    ios::sync_with_stdio(false);
    cin.tie(nullptr);

    Game game;
    bool first = false;

    string line;
    while (getline(cin, line)) {
        if (line.empty()) continue;
        istringstream iss(line);
        string cmd;
        iss >> cmd;

        if (cmd == "READY") {
            string turn;
            iss >> turn;
            first = (turn == "FIRST");
            cout << "OK" << endl;
            continue;
        }
        else if (cmd == "INIT") {
            vector<vector<int>> board;
            string row;
            while (iss >> row) {
                vector<int> rvec;
                for (char ch : row)
                    rvec.push_back(ch - '0');
                board.push_back(rvec);
            }
            game = Game(board);
            game.generateCandidateRects();
        }
        else if (cmd == "TIME") {
            int myTime, oppTime;
            iss >> myTime >> oppTime;

            vector<int> move = game.calculateMove(myTime, oppTime);
            cout << move[0] << ' ' << move[1] << ' ' << move[2] << ' ' << move[3] << '\n';
        }
        else if (cmd == "OPP") {
            int r1, c1, r2, c2, time;
            iss >> r1 >> c1 >> r2 >> c2 >> time;
            game.updateOpponentAction({r1, c1, r2, c2});
        }
        else if (cmd == "FINISH") {
            break;
        }
    }

    return 0;
}
