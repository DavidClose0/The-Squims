using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class Squim : MonoBehaviour
{
    List<Goal> goals;
    List<Action> actions;
    Action currentAction = null;
    Action tick;
    float tickLength = 5.0f;
    float actionTimer = 0;

    public TextMeshProUGUI statusText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        goals = new List<Goal>();
        goals.Add(new Goal("Hunger", 0));
        goals.Add(new Goal("Thirst", 0));
        goals.Add(new Goal("Ink Pressure", 0));
        goals.Add(new Goal("Boredom", 0));

        // "Tick": "Hunger" +1, "Thirst" +1, "Ink Pressure" +1, "Boredom" +1
        tick = new Action("Tick", 0);
        tick.effectsOnGoals.Add(goals[0], 1);
        tick.effectsOnGoals.Add(goals[1], 1);
        tick.effectsOnGoals.Add(goals[2], 1);
        tick.effectsOnGoals.Add(goals[3], 1);

        actions = new List<Action>();

        // "Eat fish": "Hunger" -4
        actions.Add(new Action("Eat fish", 5));
        actions[0].effectsOnGoals.Add(goals[0], -4);

        // "Drink water": "Thirst" -4
        actions.Add(new Action("Drink water", 5));
        actions[1].effectsOnGoals.Add(goals[1], -4);

        // "Expel ink": "Ink Pressure" -4
        actions.Add(new Action("Expel ink", 10));
        actions[2].effectsOnGoals.Add(goals[2], -4);

        // "Change color": "Boredom" -4
        actions.Add(new Action("Change color", 5));
        actions[3].effectsOnGoals.Add(goals[3], -4);

        LogGoals();
        Debug.Log("Each goal increases by 1 every " + tickLength + " seconds");
        InvokeRepeating("ApplyTick", tickLength, tickLength);
    }

    // Update is called once per frame
    void Update()
    {
        if (currentAction == null)
        {
            ChooseAction();
        }
        else
        {
            actionTimer -= Time.deltaTime;
            if (actionTimer <= 0)
            {
                currentAction.Perform();
                LogGoals();
                currentAction = null;
            }
        }

        UpdateStatusText();
    }

    void ApplyTick()
    {
        tick.Perform();
        LogGoals();
    }

    // Determine action with lowest predicted discontentment and set as current
    void ChooseAction()
    {
        Action bestAction = null;
        float bestDiscontentment = float.PositiveInfinity;

        foreach (var action in actions)
        {
            float discontentment = GetPredictedDiscontentment(action);
            if (discontentment < bestDiscontentment)
            {
                bestDiscontentment = discontentment;
                bestAction = action;
            }
        }

        currentAction = bestAction;
        actionTimer = currentAction.duration;
        Debug.Log("Starting " + currentAction.name + ", Duration: " + currentAction.duration);
    }

    float GetPredictedDiscontentment(Action action)
    {
        float discontentment = 0;
        float tickFactor = action.duration / tickLength;

        foreach (var goal in goals) {
            float predictedValue = goal.value;

            // Check effect of action on goal
            if (action.effectsOnGoals.TryGetValue(goal, out float effectValue))
            {
                predictedValue += effectValue;
            }
            
            // Check effect of tick on goal
            if (tick.effectsOnGoals.TryGetValue(goal, out float tickEffectValue))
            {
                predictedValue += tickEffectValue * tickFactor;
            }

            discontentment += goal.CalculateDiscontentment(predictedValue);
        }

        return discontentment;
    }

    void LogGoals()
    {
        string goalString = string.Join(", ", goals.Select(goal => goal.name + ": " + goal.value));
        Debug.Log(goalString);
    }

    float GetTotalDiscontentment()
    {
        float totalDiscontentment = 0;
        foreach (var goal in goals)
        {
            totalDiscontentment += goal.GetDiscontentment();
        }
        return totalDiscontentment;
    }

    void UpdateStatusText()
    {
        string statusTextString = "";

        statusTextString += "Each goal increases by 1 every " + tickLength + " seconds\n";

        foreach (var goal in goals)
        {
            statusTextString += goal.name + ": " + goal.value + "\n";
        }

        statusTextString += "Discontentment: " + GetTotalDiscontentment() + "\n";

        statusTextString += "Performing " + currentAction.name + " in " + actionTimer.ToString("0.0") + " seconds\n";

        statusText.text = statusTextString;
    }
}
