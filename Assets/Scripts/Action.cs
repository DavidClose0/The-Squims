using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Needed for FirstOrDefault

public class Action
{
    public string name;
    public float duration; // Represents different things: wait time (Sleep, Drink), total time (Color), or unused (Eat)
    public Dictionary<Goal, float> effectsOnGoals;

    public Action(string name, float duration)
    {
        this.name = name;
        effectsOnGoals = new Dictionary<Goal, float>();
        this.duration = duration;
    }

    // Apply effects to the provided list of goals
    // Made static temporarily if called directly, or pass goal list
    public void Perform(List<Goal> characterGoals)
    {
        // Debug.Log($"Performing effects for action '{name}'");
        foreach (var effect in effectsOnGoals)
        {
            // Find the matching goal in the character's list
            Goal targetGoal = characterGoals.FirstOrDefault(g => g.name == effect.Key.name); // Match by name

            if (targetGoal != null)
            {
                targetGoal.value += effect.Value;
                // Ensure goal value is non-negative
                targetGoal.value = Mathf.Max(0, targetGoal.value);
                // Debug.Log($"  Applied {effect.Value} to {targetGoal.name}. New value: {targetGoal.value}");
            }
            else
            {
                Debug.LogWarning($"Could not find goal '{effect.Key.name}' in character's goal list to apply effect from action '{name}'.");
            }
        }
    }
}