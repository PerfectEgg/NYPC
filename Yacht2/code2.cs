using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// 가능한 주사위 규칙들을 나타내는 enum
enum DiceRule
{
    ONE, TWO, THREE, FOUR, FIVE, SIX,
    CHOICE, FOUR_OF_A_KIND, FULL_HOUSE, SMALL_STRAIGHT, LARGE_STRAIGHT, YACHT
}

/// 입찰 방법을 나타내는 구조체
struct Bid
{
    /// 입찰 그룹 ('A' 또는 'B')
    public char group;
    /// 입찰 금액
    public int amount;
}

/// 주사위 배치 방법을 나타내는 구조체
struct DicePut
{
    /// 배치 규칙
    public DiceRule rule;
    /// 배치할 주사위 목록
    public int[] dice;
}

/// 게임 상태를 관리하는 클래스
class Game
{
    /// 내 팀의 현재 상태
    public GameState myState;
    /// 상대 팀의 현재 상태
    public GameState oppState;
    /// 배팅에 제시된 내 세트를 각 계산 후 해당 기대 값을 기반으로 배팅
    public BettingCalculator myBc;
    /// 배팅에 제시된 상대 세트를 각 계산 후 해당 기대 값을 기반으로 배팅
    public BettingCalculator oppBc;
    /// 각 족보나 숙제마다 무엇을 선택해야 하는 가를 정하는 클래스
    public PriorityCalculate pc;
    // 내 세트 계산을 위한 클래스
    public PriorityCalculate cmpc;
    // 상대 세트 계산을 위한 클래스
    public PriorityCalculate ompc;
    // 족보 계산을 위한 클래스
    public RuleScoreTable myRst;
    // 족보 계산을 위한 클래스
    public RuleScoreTable oppRst;
    // 저장할 내 기대값
    public int[] myExpectedArray;
    // 저장할 상대 기대 값
    public int[] oppExpectedArray;
    // 내 덱에 따른 기대 값
    public double MyExpectedValue;
    // 상대 덱에 따른 기대 값
    public double OppExpectedValue;
    // 상대가 마지막으로 냈던 배팅 금액
    public int oppLastAmount;
    // 상대가 냈던 최고 배팅 금액
    public double oppAverage;
    // 상대가 냈던 최근 배팅 금액을 저장 
    Queue<int> lastBets = new Queue<int>();
    /// 라운드 변수
    public int round;

    public Game()
    {
        myExpectedArray = new int[7];
        oppExpectedArray = new int[7];
        myState = new GameState();
        oppState = new GameState();
        myBc = new BettingCalculator(myState);
        oppBc = new BettingCalculator(oppState);
        myRst = new RuleScoreTable(myState);
        oppRst = new RuleScoreTable(oppState);
        pc = new PriorityCalculate(myState);
        cmpc = new PriorityCalculate(myState);
        ompc = new PriorityCalculate(oppState);

        oppLastAmount = 0;
        oppAverage = 0;
        round = 1;
    }

    int isLate(int round)
    {
        if (round <= 5) return 1;
        if (round <= 10) return 2;
        return 4;
    }

    int CalculateBet (int myMax, int oppMax, double myEx, double oppEx, int round, double oppAverage)
    {
        int bet;
        double scoreDiff = Math.Abs(myMax) - Math.Abs(oppMax);

        // 기본 민감도 (라운드에 따라 변화)
        double sensitivity = 1.0;
        if (round <= 4) sensitivity = 0.8;     // 초반 공격
        else if (round >= 9) sensitivity = 1.2; // 후반 안정

        // 기대값 기반 공격/방어
        double valueFactor = Math.Abs(myEx) - Math.Abs(oppEx); // +면 내가 유리, -면 불리

        // 기본 베팅
        if (valueFactor > 3 || scoreDiff > 2500)
            bet = 5000; // 강하게
        else if (valueFactor < -3 || scoreDiff < -2500)
            bet = 3500; // 방어
        else
            bet = 2000; // 중립

        // 상대 평균 베팅 패턴 방어
        if (oppAverage < 1500 && bet > oppAverage * 1.5)
            bet = (int)(oppAverage * 1.1);

        // 초반
        if (round <= 4)
            bet += (int)(Math.Max(myEx, 0) * 500);

        if (round <= 10)
        {
            if (oppAverage < 1000 && Math.Abs(oppEx) > Math.Abs(myEx))
                bet += 800;
            if (oppAverage >= 1500)
                bet += (int)(oppAverage * 0.3);
        }

        // 후반 (11~13)
        if (round >= 11)
        {
            if ((Math.Abs(myMax) - Math.Abs(oppMax) > 1000) &&
                (Math.Abs(myEx) - Math.Abs(oppEx) > 1.5))
            {
                bet = Math.Max(bet, 7000);
            }
        }

        // 라운드 민감도 적용
        bet = (int)(bet * sensitivity);

        // 최소 베팅 보정
        if (bet < 101) bet = 101;
        if (bet > 10001) bet = 10001;

        if (round == 1)
            bet = 1001;

        return bet;
    }

    // ================================ [필수 구현] ================================
    // ============================================================================
    /// 주사위가 주어졌을 때, 어디에 얼마만큼 베팅할지 정하는 함수
    /// 입찰할 그룹과 베팅 금액을 pair로 묶어서 반환
    // ============================================================================
    public Bid CalculateBid(int[] diceA, int[] diceB)
    {
        char group;
        int amount;

        // 내 기대값 계산
        myRst.ChangeState(myState);
        
        myExpectedArray = myRst.ExpectedValueCalculate(round);
        DicePut myPutA = cmpc.SimulateBestExpectedValue(myState, diceA, round, myExpectedArray);
        DicePut myPutB = cmpc.SimulateBestExpectedValue(myState, diceB, round, myExpectedArray);
        myBc.ChangeState(myState);
        int myMaxA = GameState.CalculateScore(myPutA);
        int myMaxB = GameState.CalculateScore(myPutB);
        int myMax = myMaxA >= myMaxB ? myMaxA : myMaxB;

        group = myMaxA >= myMaxB ? 'A' : 'B';

        double myEx = myBc.ExpectedAdditionCalculation(myExpectedArray, diceA, diceB);

        // 상대 기대값 계산
        oppRst.ChangeState(oppState);
        
        oppExpectedArray = oppRst.ExpectedValueCalculate(round);
        DicePut oppPutA = cmpc.SimulateBestExpectedValue(oppState, diceA, round, oppExpectedArray);
        DicePut oppPutB = cmpc.SimulateBestExpectedValue(oppState, diceB, round, oppExpectedArray);
        myBc.ChangeState(myState);
        int oppMaxA = GameState.CalculateScore(oppPutA);
        int oppMaxB = GameState.CalculateScore(oppPutB);
        int oppMax = oppMaxA >= oppMaxB ? oppMaxA : oppMaxB;

        lastBets.Enqueue(oppLastAmount);
        if(lastBets.Count > 2) lastBets.Dequeue();
        oppAverage += lastBets.Average();

        double oppEx = oppBc.ExpectedAdditionCalculation(oppExpectedArray, diceA, diceB);

        amount = CalculateBet(myMax, oppMax, myEx, oppEx, round, oppLastAmount);

        round++;
        return new Bid { group = group, amount = amount };
    }

    // ============================================================================
    /// 주어진 주사위에 대해 사용할 규칙과 주사위를 정하는 함수
    /// 사용할 규칙과 사용할 주사위의 목록을 pair로 묶어서 반환
    // ============================================================================
    public DicePut CalculatePut()
    {
        // 상태 초기화
        pc.ChangeState(myState, myExpectedArray);

        // 주사위 가져오기
        DicePut dicePut = pc.ChoosePut(round);
        return dicePut;
    }
    // ============================== [필수 구현 끝] ==============================

    /// 입찰 결과를 받아서 상태 업데이트
    public void UpdateGet(int[] diceA, int[] diceB, Bid myBid, Bid oppBid, char myGroup)
    {
        // 그룹에 따라 주사위 분배
        if (myGroup == 'A')
        {
            myState.AddDice(diceA.ToList());
            oppState.AddDice(diceB.ToList());
        }
        else
        {
            myState.AddDice(diceB.ToList());
            oppState.AddDice(diceA.ToList());
        }

        // 입찰 결과에 따른 점수 반영
        bool myBidOk = myBid.group == myGroup;
        myState.Bid(myBidOk, myBid.amount);

        char oppGroup = myGroup == 'A' ? 'B' : 'A';
        bool oppBidOk = oppBid.group == oppGroup;
        oppState.Bid(oppBidOk, oppBid.amount);

        // 상대가 마지막으로 배팅한 점수를 가져옴.
        oppLastAmount = oppBid.amount;
    }

    /// 내가 주사위를 배치한 결과 반영
    public void UpdatePut(DicePut put) { myState.UseDice(put); }

    /// 상대가 주사위를 배치한 결과 반영
    public void UpdateSet(DicePut put) { oppState.UseDice(put); }
}

/// 팀의 현재 상태를 관리하는 구조체
class GameState
{
    /// 현재 보유한 주사위 목록
    public List<int> dice;
    /// 각 규칙별 획득 점수 (사용하지 않았다면 null)
    public List<int?> ruleScore;
    /// 입찰로 얻거나 잃은 총 점수
    public int bidScore;

    // 처음에 사용하지 않은 상태로 score를 초기화
    public GameState()
    {
        dice = new List<int>();
        ruleScore = new List<int?>(new int?[12]);
        for (int i = 0; i < 12; i++) ruleScore[i] = null;
        bidScore = 0;
    }

    /// 깊은 복사(복제) 메서드
    public GameState Clone()
    {
        GameState clone = new GameState();

        // List<int> dice 복사
        clone.dice = new List<int>(this.dice);

        // List<int?> ruleScore 복사
        clone.ruleScore = new List<int?>(this.ruleScore);

        // int bidScore 복사
        clone.bidScore = this.bidScore;

        return clone;
    }

    /// 현재까지 획득한 총 점수 계산 (상단/하단 점수 + 보너스 + 입찰 점수)
    public int GetTotalScore()
    {
        int basic = 0, combination = 0, bonus = 0;

        // 기본 점수 규칙 계산 (ONE ~ SIX)
        for (int i = 0; i < 6; i++)
            if (ruleScore[i].HasValue)
                basic += ruleScore[i]!.Value;
        // 보너스 점수 계산 (기본 규칙 63000점 이상시 35000점 보너스)
        if (basic >= 63000)
            bonus += 35000;
        // 조합 점수 규칙 계산 (CHOICE ~ YACHT)
        for (int i = 6; i < 12; i++)
            if (ruleScore[i].HasValue)
                combination += ruleScore[i]!.Value;

        return basic + bonus + combination + bidScore;
    }

    /// 입찰 결과에 따른 점수 반영
    public void Bid(bool isSuccessful, int amount)
    {
        if (isSuccessful)
            bidScore -= amount;  // 성공시 베팅 금액만큼 점수 차감
        else
            bidScore += amount;  // 실패시 베팅 금액만큼 점수 획득
    }

    /// 주사위 획득
    public void AddDice(List<int> newDice)
    {
        foreach (int d in newDice) dice.Add(d);
    }

    /// 주사위 사용
    public void UseDice(DicePut put)
    {
        // 이미 사용한 규칙인지 확인
        Debug.Assert(!ruleScore[(int)put.rule].HasValue, "Rule already used");

        foreach (int d in put.dice)
        {
            // 주사위 목록에 없는 주사위가 있는지 확인하고 주사위 제거
            int index = dice.IndexOf(d);
            Debug.Assert(index != -1, "Invalid dice");
            dice.RemoveAt(index);
        }

        // 해당 규칙의 점수 계산 및 저장
        ruleScore[(int)put.rule] = CalculateScore(put);
    }

    /// 규칙에 따른 점수를 계산하는 함수
    public static int CalculateScore(DicePut put)
    {
        DiceRule rule = put.rule;
        int[] dice = put.dice;

        switch (rule)
        {
            // 기본 규칙 점수 계산 (해당 숫자의 개수 × 숫자 × 1000점)
            case DiceRule.ONE: return dice.Count(x => x == 1) * 1 * 1000;
            case DiceRule.TWO: return dice.Count(x => x == 2) * 2 * 1000;
            case DiceRule.THREE: return dice.Count(x => x == 3) * 3 * 1000;
            case DiceRule.FOUR: return dice.Count(x => x == 4) * 4 * 1000;
            case DiceRule.FIVE: return dice.Count(x => x == 5) * 5 * 1000;
            case DiceRule.SIX: return dice.Count(x => x == 6) * 6 * 1000;

            case DiceRule.CHOICE:  // 주사위에 적힌 모든 수의 합 × 1000점
                return dice.Sum() * 1000;
            case DiceRule.FOUR_OF_A_KIND:
                {  // 같은 수가 적힌 주사위가 4개 있다면, 주사위에 적힌 모든 수의 합 × 1000점, 아니면 0
                    bool ok = false;
                    for (int i = 1; i <= 6; i++)
                        if (dice.Count(x => x == i) >= 4) ok = true;
                    return ok ? dice.Sum() * 1000 : 0;
                }
            case DiceRule.FULL_HOUSE:
                {  // 3개의 주사위에 적힌 수가 서로 같고, 다른 2개의 주사위에 적힌 수도 서로 같으면 30000점, 아닐 경우 0점
                    bool pair = false, triple = false;
                    for (int i = 1; i <= 6; i++)
                    {
                        int cnt = dice.Count(x => x == i);
                        // 5개 모두 같은 숫자일 때도 인정
                        if (cnt == 2 || cnt == 5) pair = true;
                        if (cnt == 3 || cnt == 5) triple = true;
                    }
                    return (pair && triple) ? dice.Sum() * 1000 : 0;
                }
            case DiceRule.SMALL_STRAIGHT:
                {  // 4개의 주사위에 적힌 수가 1234, 2345, 3456중 하나로 연속되어 있을 때, 15000점, 아닐 경우 0점
                    bool e1 = dice.Count(x => x == 1) > 0;
                    bool e2 = dice.Count(x => x == 2) > 0;
                    bool e3 = dice.Count(x => x == 3) > 0;
                    bool e4 = dice.Count(x => x == 4) > 0;
                    bool e5 = dice.Count(x => x == 5) > 0;
                    bool e6 = dice.Count(x => x == 6) > 0;
                    bool ok = (e1 && e2 && e3 && e4) || (e2 && e3 && e4 && e5) ||
                              (e3 && e4 && e5 && e6);
                    return ok ? 15000 : 0;
                }
            case DiceRule.LARGE_STRAIGHT:
                {  // 5개의 주사위에 적힌 수가 12345, 23456중 하나로 연속되어 있을 때, 30000점, 아닐 경우 0점
                    bool e1 = dice.Count(x => x == 1) > 0;
                    bool e2 = dice.Count(x => x == 2) > 0;
                    bool e3 = dice.Count(x => x == 3) > 0;
                    bool e4 = dice.Count(x => x == 4) > 0;
                    bool e5 = dice.Count(x => x == 5) > 0;
                    bool e6 = dice.Count(x => x == 6) > 0;
                    bool ok = (e1 && e2 && e3 && e4 && e5) || (e2 && e3 && e4 && e5 && e6);
                    return ok ? 30000 : 0;
                }
            case DiceRule.YACHT:
                {  // 5개의 주사위에 적힌 수가 모두 같을 때 50000점, 아닐 경우 0점
                    bool ok = false;
                    for (int i = 1; i <= 6; i++)
                        if (dice.Count(x => x == i) == 5) ok = true;
                    return ok ? 50000 : 0;
                }
        }
        Debug.Assert(false);
        return 0;
    }
}

/// 배팅을 위한 계산
class BettingCalculator
{
    private GameState gs;

    public BettingCalculator(GameState gameState)
    {
        //complete = new bool[12];
        gs = gameState;
    }

    public void ChangeState(GameState gameState)
    {
        gs = gameState;
    }

    public double ExpectedAdditionCalculation(int[] expectedValue, int[] diceA, int[] diceB)
    {
        double mySum = gs.dice.Sum(d => expectedValue[d]);   // 내 인벤토리 반영
        double setA = diceA.Sum(d => expectedValue[d]);
        double setB = diceA.Sum(d => expectedValue[d]);
        double result = (setA + mySum) - (setB + mySum);
        // 기대치 차이: 내(합+합쳐질 가능성) - 상대(합+합쳐질 가능성)
        return result;
    }
    
}

/// 우선 순위 계산을 위한 클래스
class PriorityCalculate
{
    /// 주사위를 가져오기 위한 내 게임 상태
    private GameState gs;
    /// 족보 및 숙제 완성 여부 
    private bool[] complete;
    private int[] count;
    private int bonus;
    private int[] ea;

    public PriorityCalculate(GameState gameState)
    {
        complete = new bool[12];
        count = new int[7];
        gs = gameState;
        bonus = 0;
    }

    public void ChangeState(GameState gameState, int[] ExpectedArray)
    {
        gs = gameState;
        ea = ExpectedArray;
    }

    // 족보 별 완성 여부
    public void CompleteCalculate()
    {
        Array.Fill(complete, false);
        for (int i = 0; i < gs.ruleScore.Count; i++)
            complete[i] = gs.ruleScore[i].HasValue;
    }

    public void CountCalculate()
    {
        Array.Fill(count, 0);
        foreach (int d in gs.dice) count[d]++;
    }
    
    public DicePut SimulateBestExpectedValue(GameState state, int[] dice, int round, int[] ExpectedArray)
    {
        PriorityCalculate priorityCalculate = new PriorityCalculate(state);
        GameState cloneState = state.Clone();

        cloneState.dice.AddRange(dice); // 가상 배치
        priorityCalculate.ChangeState(cloneState, ExpectedArray);
        DicePut value = priorityCalculate.ChoosePut(round);

        return value;
    }


    // 순서에 따라 우선적으로 처리하는 코드
    public DicePut ChoosePut(int round)
    {
        List<int> dice = gs.dice;
        CountCalculate();
        CompleteCalculate();

        // 1. 포카드 이상이 먼저 나올 경우 보너스 6부터 처리
        if (!complete[(int)DiceRule.SIX] && IsFourOfAKind(dice) && count[6] == 5)
        {
            complete[(int)DiceRule.SIX] = true;
            var six = GetSameNumberDice(dice, 5);
            return new DicePut { rule = DiceRule.SIX, dice = six };
        }

        // 2. 야추 선별
        if (!complete[(int)DiceRule.YACHT] && IsYacht(dice))
        {
            complete[(int)DiceRule.YACHT] = true;
            var yacht = GetSameNumberDice(dice, 5);
            return new DicePut { rule = DiceRule.YACHT, dice = yacht };
        }

        // 3. 보너스 슈퍼 고점 선별
        if (!complete[(int)DiceRule.SIX] && IsBonus(6))
        {
            complete[(int)DiceRule.SIX] = true;
            var b = ChooseBonusDice(6);
            bonus += CountReturn(6) * 6;
            return new DicePut { rule = DiceRule.SIX, dice = b };
        }
        if (!complete[(int)DiceRule.FIVE] && IsBonus(5))
        {
            complete[(int)DiceRule.FIVE] = true;
            var b = ChooseBonusDice(5);
            bonus += CountReturn(5) * 5;
            return new DicePut { rule = DiceRule.FIVE, dice = b };
        }

        // 4. 보너스 고점 선별
        if (!complete[(int)DiceRule.FOUR] && IsBonus(4))
        {
            complete[(int)DiceRule.FOUR] = true;
            var b = ChooseBonusDice(4);
            bonus += CountReturn(4) * 4;
            return new DicePut { rule = DiceRule.FOUR, dice = b };
        }
        if (!complete[(int)DiceRule.THREE] && IsBonus(3))
        {
            complete[(int)DiceRule.THREE] = true;
            var b = ChooseBonusDice(3);
            bonus += CountReturn(3) * 3;
            return new DicePut { rule = DiceRule.THREE, dice = b };
        }

        // 5. 풀하우스 선별
        if (!complete[(int)DiceRule.FULL_HOUSE] && IsFullHouse(dice) && IsHighscoreFullHouse())
        {
            complete[(int)DiceRule.FULL_HOUSE] = true;
            var fh = GetHighscoreFullHouseDice();
            return new DicePut { rule = DiceRule.FULL_HOUSE, dice = fh };
        }

        // 6. 포카드 선별
        if (!complete[(int)DiceRule.FOUR_OF_A_KIND] && IsFourOfAKind(dice) && IsHighscoreFourOfAKind())
        {
            complete[(int)DiceRule.FOUR_OF_A_KIND] = true;
            var fok = GetHighscoreFourOfAKindDice();
            return new DicePut { rule = DiceRule.FOUR_OF_A_KIND, dice = fok };
        }

        // 7. 라스 선별
        if (!complete[(int)DiceRule.LARGE_STRAIGHT] && IsLargeStraight(dice))
        {
            complete[(int)DiceRule.LARGE_STRAIGHT] = true;
            var ls = GetLargeStraightDice(dice);
            return new DicePut { rule = DiceRule.LARGE_STRAIGHT, dice = ls };
        }

        // 8. 보너스 2 선별
        if (!complete[(int)DiceRule.TWO] && IsBonus(2) && bonus <= 62)
        {
            complete[(int)DiceRule.TWO] = true;
            var b = ChooseBonusDice(2);
            return new DicePut { rule = DiceRule.TWO, dice = b };
        }

        // 9. 스스 선별
        if (!complete[(int)DiceRule.SMALL_STRAIGHT] && IsSmallStraight(dice))
        {
            complete[(int)DiceRule.SMALL_STRAIGHT] = true;
            var ss = GetSmallStraightDice(dice);
            return new DicePut { rule = DiceRule.SMALL_STRAIGHT, dice = ss };
        }

        // 10. 아무것도 없을 경우 보너스 짬처리
        if (!complete[(int)DiceRule.SIX] && IsNotBadBonus(6))
        {
            complete[(int)DiceRule.SIX] = true;
            var b = ChooseBonusDice(6);
            bonus += CountReturn(6) * 6;
            return new DicePut { rule = DiceRule.SIX, dice = b };
        }
        if (!complete[(int)DiceRule.FIVE] && IsNotBadBonus(5))
        {
            complete[(int)DiceRule.FIVE] = true;
            var b = ChooseBonusDice(5);
            bonus += CountReturn(5) * 5;
            return new DicePut { rule = DiceRule.FIVE, dice = b };
        }
        if (!complete[(int)DiceRule.FOUR] && IsNotBadBonus(4))
        {
            complete[(int)DiceRule.FOUR] = true;
            var b = ChooseBonusDice(4);
            bonus += CountReturn(4) * 4;
            return new DicePut { rule = DiceRule.FOUR, dice = b };
        }
        if (!complete[(int)DiceRule.THREE] && IsNotBadBonus(3))
        {
            complete[(int)DiceRule.THREE] = true;
            var b = ChooseBonusDice(3);
            bonus += CountReturn(3) * 3;
            return new DicePut { rule = DiceRule.THREE, dice = b };
        }
        if (!complete[(int)DiceRule.TWO] && IsNotBadBonus(2))
        {
            complete[(int)DiceRule.TWO] = true;
            var b = ChooseBonusDice(2);
            bonus += CountReturn(2) * 2;
            return new DicePut { rule = DiceRule.TWO, dice = b };
        }

        // 11. 고점 초이스 
        if (!complete[(int)DiceRule.CHOICE] && IsHighChoice(dice))
        {
            complete[(int)DiceRule.CHOICE] = true;
            var ch = GetHighChoice(dice);
            return new DicePut { rule = DiceRule.CHOICE, dice = ch };
        }

        // 8라운드 이후 마무리 작업
        if (round >= 6)
        {
            // 13. 보너스 1 선별
            if (!complete[(int)DiceRule.ONE] && IsBonus(1) && bonus <= 62)
            {
                complete[(int)DiceRule.ONE] = true;
                var b = ChooseBonusDice(1);
                return new DicePut { rule = DiceRule.ONE, dice = b };
            }

            // 14. 풀하우스 아무거나 짬처리
            if (!complete[(int)DiceRule.FULL_HOUSE] && IsFullHouse(dice))
            {
                complete[(int)DiceRule.FULL_HOUSE] = true;
                var fh = GetFullHouseDice();
                return new DicePut { rule = DiceRule.FULL_HOUSE, dice = fh };
            }

            // 15. 포카드 아무거나 짬처리
            if (!complete[(int)DiceRule.FOUR_OF_A_KIND] && IsFourOfAKind(dice))
            {
                complete[(int)DiceRule.FOUR_OF_A_KIND] = true;
                var fok = GetFourOfAKindDice();
                return new DicePut { rule = DiceRule.FOUR_OF_A_KIND, dice = fok };
            }

            // 16. 저점 초이스 
            if (!complete[(int)DiceRule.CHOICE])
            {
                complete[(int)DiceRule.CHOICE] = true;
                var ch = GetHighChoice(dice);
                return new DicePut { rule = DiceRule.CHOICE, dice = ch };
            }
            
            // 12. 1기 방패 빼기
            if (!complete[(int)DiceRule.ONE])
            {
                complete[(int)DiceRule.ONE] = true;
                List<int> d1 = TakeLeastValuedDice();

                return new DicePut { rule = DiceRule.ONE, dice = d1.ToArray() };
            }
        }

        // 17. 진짜 처리할게 없을 경우 우선 순위 별로 처리
        int rule = PriorityThrow();
        complete[rule] = true;

        List<int> d = TakeLeastValuedDice();
        return new DicePut { rule = (DiceRule)rule, dice = d.ToArray() };
    }

    // 만들기 어려운 순서대로 던짐
    int PriorityThrow()
    {
        int[] priorityOrder =
        {
            (int)DiceRule.ONE,
            (int)DiceRule.TWO,
            (int)DiceRule.FULL_HOUSE,
            (int)DiceRule.FOUR_OF_A_KIND,
            (int)DiceRule.YACHT,
            (int)DiceRule.LARGE_STRAIGHT,
            (int)DiceRule.SMALL_STRAIGHT,
            (int)DiceRule.THREE,
            (int)DiceRule.FOUR,
            (int)DiceRule.FIVE,
            (int)DiceRule.SIX,
            (int)DiceRule.CHOICE
        };

        foreach (var rule in priorityOrder)
        {
            if (!complete[rule])
                return rule;
        }

        // 모든 규칙이 완료된 경우 안전값 반환 (기본적으로 CHOICE)
        return (int)DiceRule.CHOICE;
    }

    // 기대 값에서 가장 낮은 순위를 반환하는 함수
    public List<int> TakeLeastValuedDice()
    {
        var result = new List<int>();
        // 1~6 눈에 대해 기대값 오름차순으로 정렬
        var diceByValueAsc = Enumerable.Range(1, 6)
                            .OrderBy(i => ea[i])
                            .ToList();

        foreach (var dieValue in diceByValueAsc)
        {
            int available = count[dieValue];
            if (available == 0) continue;

            int toTake = Math.Min(available, 5 - result.Count);

            for (int i = 0; i < toTake; i++)
                result.Add(dieValue);

            // count 차감
            count[dieValue] -= toTake;

            if (result.Count == 5)
                break;
        }

        // 선택한 주사위 만큼 count도 차감해야 한다면 호출하는 쪽에서 처리하거나 여기서 처리 가능
        return result;
    }

    // 라지 스트레이트 확인
    bool IsLargeStraight(List<int> dice)
    {
        var unique = dice.Distinct().OrderBy(x => x).ToArray();
        int[][] patterns = [[1, 2, 3, 4, 5], [2, 3, 4, 5, 6]];
        return patterns.Any(p => p.All(v => unique.Contains(v)));
    }

    // 라지 스트레이트 반환
    private int[]? GetLargeStraightDice(List<int> dice)
    {
        var temp = new List<int>(dice); // 인벤토리 복사
        int[][] patterns = [[1, 2, 3, 4, 5], [2, 3, 4, 5, 6]];

        foreach (var p in patterns)
        {
            // 패턴의 모든 숫자가 존재하는지 (부분집합)
            if (!p.All(v => temp.Contains(v))) continue;

            var result = new List<int>();
            var working = new List<int>(temp);
            // 패턴의 각 숫자 하나씩 채움 (인벤토리의 실제 인스턴스 사용)
            foreach (int v in p)
            {
                int idx = working.IndexOf(v);
                if (idx == -1) { result = null; break; } // 안전장치
                result.Add(working[idx]);
                working.RemoveAt(idx);
            }
            if (result != null)
            {
                // Large straight는 5개 정확히 필요하므로 result.Count == 5
                // (이 구현은 패턴 길이가 5이므로 그대로 5개)
                return result.ToArray();
            }
        }

        return null;
    }

    // 스몰 스트레이트 확인
    bool IsSmallStraight(List<int> dice)
    {
        var unique = dice.Distinct().OrderBy(x => x).ToArray();
        int[][] patterns = [[1, 2, 3, 4], [2, 3, 4, 5], [3, 4, 5, 6]];
        return patterns.Any(p => p.All(v => unique.Contains(v)));
    }

    // 스몰 스트레이트 반환
    private int[]? GetSmallStraightDice(List<int> dice)
    {
        var temp = new List<int>(dice);
        int[][] patterns = [[1, 2, 3, 4], [2, 3, 4, 5], [3, 4, 5, 6]];

        foreach (var p in patterns)
        {
            if (!p.All(v => temp.Contains(v))) continue;

            var result = new List<int>();
            var working = new List<int>(temp);
            foreach (int v in p)
            {
                int idx = working.IndexOf(v);
                if (idx == -1) { result = null; break; }
                result.Add(working[idx]);
                working.RemoveAt(idx);
            }
            if (result != null)
            {
                // Small-straight는 규칙상 4개만 있어도 되지만, 일관되게 5개 제출하려면
                // 남은 주사위 중 가장 작은 수를 하나 더 붙여서 5개로 만듦
                if (result.Count < 5 && working.Count > 0)
                {
                    result.Add(working.Min());
                }
                return result.ToArray();
            }
        }

        return null;
    }

    // 포카드 확인
    bool IsFourOfAKind(List<int> dice) => count.Any(c => c >= 4);

    // 같은 숫자의 합이 20이 넘는 수가 있는 지 확인
    bool IsHighscoreFourOfAKind() => Enumerable.Range(4, 3).Any(i => count[i] >= 4);

    // 고점 포카드롤 주사위로 반환
    int[] GetHighscoreFourOfAKindDice()
    {
        // 4~6 중에서 4개 이상 있는 눈 찾기
        int fourKind = Enumerable.Range(4, 3)
            .First(i => count[i] >= 4);

        count[fourKind] -= 4;

        // 남은 숫자 중에서 가장 큰 수 찾기
        int kicker = Enumerable.Range(1, 6)
            .Where(i => count[i] > 0)
            .OrderByDescending(i => i)
            .FirstOrDefault();

        count[fourKind] += 4;

        return Enumerable.Repeat(fourKind, 4)
            .Concat([kicker])
            .ToArray();
    }

    // 포카드를 주사위로 반환
    int[] GetFourOfAKindDice()
    {
        // 1~6 중에서 4개 이상 있는 눈 찾기
        int fourKind = Enumerable.Range(1, 6)
            .First(i => count[i] >= 4);

        count[fourKind] -= 4;

        // 남은 숫자 중에서 가장 큰 수 찾기
        int kicker = Enumerable.Range(1, 6)
            .Where(i => count[i] > 0)
            .OrderByDescending(i => i)
            .FirstOrDefault();

        count[fourKind] += 4;

        return Enumerable.Repeat(fourKind, 4)
            .Concat([kicker])
            .ToArray();
    }

    // 풀하우스 확인
    bool IsFullHouse(List<int> dice)
    {
        // 세 개 이상의 눈을 찾음
        int threeKind = Enumerable.Range(1, 6)
            .FirstOrDefault(i => count[i] >= 3);
        if (threeKind == 0) return false;

        // threeKind에서 3개 제거
        count[threeKind] -= 3;

        // 두 개 이상의 눈이 남아 있는지 확인
        bool hasPair = count.Any(c => c >= 2);

        // 복구
        count[threeKind] += 3;

        return hasPair;
    }

    // 풀하우스의 합이 20이 넘는 수가 있는 지 확인
    bool IsHighscoreFullHouse()
    {
        // 3개 이상 있는 눈 찾기
        int threeKind = Enumerable.Range(1, 6)
            .FirstOrDefault(i => count[i] >= 3);

        if (threeKind == 0) return false;

        count[threeKind] -= 3;

        // 나머지에서 2개 이상 있는 눈 찾기
        int pair = Enumerable.Range(1, 6)
            .FirstOrDefault(i => count[i] >= 2);

        count[threeKind] += 3;

        if (pair == 0) return false;

        // 합계가 20 이상인지 확인
        int sum = threeKind * 3 + pair * 2;
        return sum >= 20;
    }

    // 고점 플하우스를 주사위로 반환
    int[] GetHighscoreFullHouseDice()
    {
        // 4~6 중에서 3개 이상 있는 눈 찾기
        int threeKind = Enumerable.Range(4, 3)
            .First(i => count[i] >= 3);

        count[threeKind] -= 3;

        // 나머지 주사위 중 2개 이상 있는 눈 찾기 (높은 눈 우선)
        int pair = Enumerable.Range(6, 1)
            .Concat(Enumerable.Range(1, 5).Reverse())
            .First(i => count[i] >= 2);

        count[threeKind] += 3;

        return Enumerable.Repeat(threeKind, 3)
            .Concat(Enumerable.Repeat(pair, 2))
            .ToArray();
    }

    // 풀하우스를 주사위로 반환
    int[] GetFullHouseDice()
    {
        // 세 개 이상의 눈 찾기 (높은 수 우선)
        int threeKind = Enumerable.Range(6, 1)
            .Concat(Enumerable.Range(1, 5).Reverse())
            .First(i => count[i] >= 3);

        count[threeKind] -= 3;

        // 두 개 이상의 눈 찾기 (높은 수 우선)
        int pair = Enumerable.Range(6, 1)
            .Concat(Enumerable.Range(1, 5).Reverse())
            .First(i => count[i] >= 2);

        count[threeKind] += 3;

        return Enumerable.Repeat(threeKind, 3)
            .Concat(Enumerable.Repeat(pair, 2))
            .ToArray();
    }

    // 보너스가 되는 지 확인
    bool IsBonus(int num) => count[num] >= 3;

    // 보너스는 아니지만 2개는 먹는 지 확인
    bool IsNotBadBonus(int num) => count[num] >= 2;

    // 보너스 점수 63을 위한 반환
    int CountReturn(int num) => count[num];

    // 보너스 주사위를 반환
    int[] ChooseBonusDice(int num)
    {
        var bonusDice = new List<int>();

        // 해당 눈 개수만큼 먼저 채움
        while (count[num] > 0 && bonusDice.Count < 5)
        {
            bonusDice.Add(num);
            count[num]--;
        }

        // 나머지 자리는 기대값 오름차순으로 채움
        var diceByValueAsc = Enumerable.Range(1, 6)
                                    .Where(i => i != num)
                                    .OrderBy(i => ea[i]) // 기대값 낮은 것부터
                                    .ToList();

        foreach (var dieValue in diceByValueAsc)
        {
            while (count[dieValue] > 0 && bonusDice.Count < 5)
            {
                bonusDice.Add(dieValue);
                count[dieValue]--;
            }
            if (bonusDice.Count == 5) break;
        }

        // 혹시 못 채웠으면 안전값으로 1 채움
        while (bonusDice.Count < 5)
            bonusDice.Add(1);

        return bonusDice.ToArray();
    }

    // 20을 넘기는 초이스 확인
    bool IsHighChoice(List<int> dice)
    {
        var top5 = dice.OrderByDescending(x => x).Take(5);
        return top5.Sum() >= 20;
    }

    // 20을 넘기는 초이스 반환
    int[] GetHighChoice(List<int> dice)
    {
        return dice.OrderByDescending(x => x).Take(5).ToArray();
    }

    // 야추 확인
    bool IsYacht(List<int> dice)
    {
        return count.Any(c => c >= 5);
    }

    // 같은 주사위 눈 반환
    private int[]? GetSameNumberDice(List<int> dice, int n)
    {
        for (int i = 1; i <= 6; i++)
        {
            if (count[i] >= n)
                return Enumerable.Repeat(i, n).ToArray();
        }
        return null;
    }
}

class RuleScoreTable
{
    private GameState gs;
    private bool[] complete;
    private int[] count;
    private int bonus;
    private bool Negative;

    public RuleScoreTable(GameState gameState)
    {
        complete = new bool[12];
        count = new int[7];
        gs = gameState;
        Negative = false;
    }

    public void ChangeState(GameState gameState)
    {
        gs = gameState;
    }

    // 주사위 별 중복 값
    public void CountCalculate()
    {
        Array.Fill(count, 0);
        foreach (int d in gs.dice) count[d]++;
    }

    // 족보 별 완성 여부
    public void CompleteCalculate()
    {
        Array.Fill(complete, false);
        for (int i = 0; i < gs.ruleScore.Count; i++)
            complete[i] = gs.ruleScore[i].HasValue;
    }

    // 보너스 계산
    public void BonusCalculate()
    {
        bonus = 0;
        for (int i = 0; i < 6; i++)
        {
            if (gs.ruleScore[i] != null)
                bonus += (int)(gs.ruleScore[i] / 1000);
        }
    }

    public void NegativeCalculate()
    { 
        for (int i = 0; i < 6; i++)
        {
            if (gs.ruleScore[i] != null)
                Negative = ((int)(gs.ruleScore[i] / 1000)) / 1 + i < 3;
        }
    }

    // 각 족보 별 완성 여부에 따른 기대 값 부여
    public int[] ExpectedValueCalculate(int round)
    {
        int[] result = new int[7];
        BonusCalculate();
        NegativeCalculate();

        void AddHighDiceWeight()
        {
            result[4] += round >= 7 ? 2 : 1;
            result[5] += round >= 7 ? 2 : 1;
            result[6] += round >= 7 ? 2 : 1;
        }

        // Yacht 기대값
        if (!complete[(int)DiceRule.YACHT])
        {
            int maxCount = count.Max();
            for (int eye = 1; eye <= 6; eye++)
            {
                if (count[eye] == maxCount)
                    result[eye] += round >= 7 ? 6 : 3;
            }
        }

        if (bonus <= 50)
            AddHighDiceWeight();

        // 상단 섹션 남은 항목
        if (!complete[(int)DiceRule.SIX])
        {
            if (Negative)
                result[6] += 3;
            else
                result[6]++;
        }
        if (!complete[(int)DiceRule.FIVE])
        {
            if (Negative)
                result[5] += 3;
            else
                result[5]++;
        }
        if (!complete[(int)DiceRule.FOUR])
        {
            if (Negative)
                result[4] += 3;
            else
                result[4]++;
        }
        if (!complete[(int)DiceRule.THREE]) 
        {
            if (Negative)
                result[3] += 3;
            else
                result[3]++;
        }

        // 보너스 막판 조정 (거의 다 채웠을 경우)
        if (!complete[(int)DiceRule.TWO] && bonus >= 54)
            result[2] += 3;
        if (!complete[(int)DiceRule.ONE] && bonus >= 60)
            result[1] += 3;

        // Choice, 포카드, 풀하우스는 큰 눈 선호
        if (!complete[(int)DiceRule.CHOICE])
            AddHighDiceWeight();
        if (!complete[(int)DiceRule.FOUR_OF_A_KIND])
            AddHighDiceWeight();
        if (!complete[(int)DiceRule.FULL_HOUSE])
            AddHighDiceWeight();

        // 스트레이트 류가 안 나갔을 때 하는 선호
        if (!complete[(int)DiceRule.LARGE_STRAIGHT] && round >= 7)
        {
            result[1] += 3;
            result[2] += 3;
        }
        if (!complete[(int)DiceRule.SMALL_STRAIGHT] && round >= 7)
        {
            result[1] += 2;
            result[2] += 2;
        }

        return result;
    }
    
}

/// 표준 입력을 통해 명령어를 처리하는 메인 함수
class Program
{
    /// 입출력을 위해 규칙 enum을 문자열로 변환
    static string ToString(DiceRule rule)
    {
        switch (rule)
        {
            case DiceRule.ONE: return "ONE";
            case DiceRule.TWO: return "TWO";
            case DiceRule.THREE: return "THREE";
            case DiceRule.FOUR: return "FOUR";
            case DiceRule.FIVE: return "FIVE";
            case DiceRule.SIX: return "SIX";
            case DiceRule.CHOICE: return "CHOICE";
            case DiceRule.FOUR_OF_A_KIND: return "FOUR_OF_A_KIND";
            case DiceRule.FULL_HOUSE: return "FULL_HOUSE";
            case DiceRule.SMALL_STRAIGHT: return "SMALL_STRAIGHT";
            case DiceRule.LARGE_STRAIGHT: return "LARGE_STRAIGHT";
            case DiceRule.YACHT: return "YACHT";
        }
        Debug.Assert(false, "Invalid Dice Rule");  // 올바르지 않은 주사위 규칙
        return "";
    }

    /// 문자열을 규칙 enum으로 변환
    static DiceRule FromString(string s)
    {
        if (s == "ONE") return DiceRule.ONE;
        if (s == "TWO") return DiceRule.TWO;
        if (s == "THREE") return DiceRule.THREE;
        if (s == "FOUR") return DiceRule.FOUR;
        if (s == "FIVE") return DiceRule.FIVE;
        if (s == "SIX") return DiceRule.SIX;
        if (s == "CHOICE") return DiceRule.CHOICE;
        if (s == "FOUR_OF_A_KIND") return DiceRule.FOUR_OF_A_KIND;
        if (s == "FULL_HOUSE") return DiceRule.FULL_HOUSE;
        if (s == "SMALL_STRAIGHT") return DiceRule.SMALL_STRAIGHT;
        if (s == "LARGE_STRAIGHT") return DiceRule.LARGE_STRAIGHT;
        if (s == "YACHT") return DiceRule.YACHT;
        Debug.Assert(false, "Invalid Dice Rule");  // 올바르지 않은 주사위 규칙
        return DiceRule.ONE;
    }
    /// 메인 함수 - 게임 루프를 실행
    static void Main(string[] args)
    {
        Game game = new Game();

        // 입찰 라운드에서 나온 주사위들
        int[] diceA = new int[5], diceB = new int[5];
        // 내가 마지막으로 한 입찰 정보
        Bid myBid = new Bid();

        while (true)
        {
            string? line = Console.ReadLine();
            if (string.IsNullOrEmpty(line)) continue;

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0];

            switch (command)
            {
                case "READY":
                    // 게임 시작
                    Console.WriteLine("OK");
                    Console.Out.Flush();
                    break;

                case "ROLL":
                    // 주사위 굴리기 결과 받기
                    string strA = parts[1], strB = parts[2];
                    for (int i = 0; i < strA.Length; i++) diceA[i] = strA[i] - '0';
                    for (int i = 0; i < strB.Length; i++) diceB[i] = strB[i] - '0';
                    if (game == null) game = new Game();
                    myBid = game.CalculateBid(diceA, diceB);
                    Console.WriteLine($"BID {myBid.group} {myBid.amount}");
                    Console.Out.Flush();
                    break;

                case "GET":
                    // 주사위 받기
                    char getGroup = parts[1][0];
                    char oppGroup = parts[2][0];
                    int oppScore = int.Parse(parts[3]);
                    game.UpdateGet(diceA, diceB, myBid, new Bid { group = oppGroup, amount = oppScore }, getGroup);
                    break;

                case "SCORE":
                    // 주사위 골라서 배치하기
                    DicePut put = game.CalculatePut();
                    game.UpdatePut(put);
                    Console.Write($"PUT {ToString(put.rule)} ");
                    foreach (int d in put.dice) Console.Write(d);
                    Console.WriteLine();
                    Console.Out.Flush();
                    break;

                case "SET":
                    // 상대의 주사위 배치
                    string rule = parts[1], str = parts[2];
                    int[] dice = str.Select(c => c - '0').ToArray();
                    game.UpdateSet(new DicePut { rule = FromString(rule), dice = dice });
                    break;

                case "FINISH":
                    // 게임 종료
                    return;

                default:
                    // 잘못된 명령 처리
                    Console.Error.WriteLine($"Invalid command: {command}");
                    Environment.Exit(1);
                    break;
            }
        }
    }
}