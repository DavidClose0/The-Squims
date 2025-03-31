using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Squim : MonoBehaviour
{
    List<Goal> goals;
    List<Action> availableActions;
    Action currentAction = null;
    Action tick;
    float tickLength = 1.0f;

    private enum SquimState { Idle_ChoosingAction, MovingToTarget, PerformingAction_Waiting }
    private SquimState currentState = SquimState.Idle_ChoosingAction;

    private NavMeshAgent agent;
    private GameObject targetObject = null;
    private float actionTimer = 0;
    private const float WaterProximityThreshold = 2.0f;

    public float fishCheckInterval = 0.5f;
    private float timeSinceLastFishCheck = 0f;

    public Transform bedWaypoint;
    public Transform waterCoolerWaypoint;
    public List<Material> availableMaterials;
    private Renderer squimRenderer;
    private static GameObject occupantOfBed = null; // Static: shared by all Squims
    private UIManager uiManager;

    private const string EatFishActionName = "Eat fish";
    private const string DrinkWaterActionName = "Drink water";
    private const string SleepInBedActionName = "Sleep in bed";
    private const string ChangeColorActionName = "Change color";
    private const string TickActionName = "Tick";

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        squimRenderer = GetComponentInChildren<Renderer>();
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        goals = new List<Goal> { new Goal("Hunger", 0), new Goal("Thirst", 0), new Goal("Fatigue", 0), new Goal("Boredom", 0) };

        tick = new Action(TickActionName, 0);
        var hungerGoal = goals.First(g => g.name == "Hunger");
        var thirstGoal = goals.First(g => g.name == "Thirst");
        var fatigueGoal = goals.First(g => g.name == "Fatigue");
        var boredomGoal = goals.First(g => g.name == "Boredom");

        tick.effectsOnGoals.Add(hungerGoal, 1);
        tick.effectsOnGoals.Add(thirstGoal, 1);
        tick.effectsOnGoals.Add(fatigueGoal, 1);
        tick.effectsOnGoals.Add(boredomGoal, 1);

        availableActions = new List<Action>();

        availableActions.Add(new Action(EatFishActionName, 0));
        availableActions.Last().effectsOnGoals.Add(hungerGoal, -20);
        availableActions.Last().effectsOnGoals.Add(thirstGoal, 5);

        availableActions.Add(new Action(DrinkWaterActionName, 2));
        availableActions.Last().effectsOnGoals.Add(thirstGoal, -20);

        availableActions.Add(new Action(SleepInBedActionName, 10));
        availableActions.Last().effectsOnGoals.Add(fatigueGoal, -50);

        availableActions.Add(new Action(ChangeColorActionName, 5));
        availableActions.Last().effectsOnGoals.Add(boredomGoal, -20);

        InvokeRepeating("ApplyTick", tickLength, tickLength);

        if (bedWaypoint == null) Debug.LogError($"[{gameObject.name}] Bed Waypoint missing!");
        if (waterCoolerWaypoint == null) Debug.LogError($"[{gameObject.name}] Water Cooler Waypoint missing!");
        if (availableMaterials == null || availableMaterials.Count == 0) Debug.LogError($"[{gameObject.name}] Available materials missing!");
        if (squimRenderer == null) Debug.LogError($"[{gameObject.name}] Renderer missing!");

        uiManager = FindAnyObjectByType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogError($"[{gameObject.name}] Could not find UIManager in scene!");
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case SquimState.Idle_ChoosingAction: ChooseAction(); break;
            case SquimState.MovingToTarget: HandleMovement(); break;
            case SquimState.PerformingAction_Waiting: HandleWaitingAction(); break;
        }
    }

    void ApplyTick()
    {
        tick.Perform(goals);
        CheckDeathCondition();
    }

    void ChooseAction()
    {
        Action bestAction = null;
        float bestDiscontentment = float.PositiveInfinity;
        GameObject potentialTarget = null;

        Fish[] currentFish = FindObjectsByType<Fish>(FindObjectsSortMode.None);
        bool fishAvailable = currentFish.Length > 0;
        bool bedAvailable = occupantOfBed == null || occupantOfBed == this.gameObject; // Allow re-eval if we occupy

        foreach (var action in availableActions)
        {
            if (action.name == EatFishActionName && !fishAvailable) continue;
            if (action.name == SleepInBedActionName && !bedAvailable) continue;

            GameObject actionTarget = null;
            float actionDuration = GetEffectiveActionDuration(action, ref actionTarget, currentFish);

            if (actionDuration == float.PositiveInfinity) continue; // Cannot perform
            if (action.name == EatFishActionName && actionTarget == null && fishAvailable) continue; // Edge case: fish exists but target finding failed

            float discontentment = GetPredictedDiscontentment(action, actionDuration);

            if (discontentment < bestDiscontentment)
            {
                bestDiscontentment = discontentment;
                bestAction = action;
                potentialTarget = actionTarget;
            }
        }

        if (bestAction != null)
        {
            currentAction = bestAction;
            targetObject = potentialTarget;
            Debug.Log($"[{gameObject.name}] Chose Action: '{currentAction.name}'. Predicted Discontentment: {bestDiscontentment:F1}");

            if (currentAction.name == ChangeColorActionName)
            {
                currentState = SquimState.PerformingAction_Waiting;
                actionTimer = currentAction.duration;
                if (agent.isOnNavMesh) agent.isStopped = true;
                // Debug.Log($"[{gameObject.name}] Starting '{currentAction.name}', waiting {actionTimer}s.");
            }
            else if (targetObject != null) // Eat Fish, Drink Water, Sleep
            {
                if (agent.isOnNavMesh && agent.enabled)
                {
                    agent.SetDestination(targetObject.transform.position);
                    agent.isStopped = false;
                    currentState = SquimState.MovingToTarget;
                    timeSinceLastFishCheck = 0f;
                    // Debug.Log($"[{gameObject.name}] Moving to '{targetObject.name}' for '{currentAction.name}'.");

                    if (currentAction.name == SleepInBedActionName)
                    {
                        if (occupantOfBed == null || occupantOfBed == this.gameObject)
                        {
                            occupantOfBed = this.gameObject;
                            // Debug.Log($"[{gameObject.name}] Reserved/Re-reserved bed.");
                        }
                        else
                        {
                            Debug.LogError($"[{gameObject.name}] Bed reservation conflict! Occupied by {occupantOfBed.name}. Resetting.");
                            ResetState(); return;
                        }
                    }
                }
                else { Debug.LogError($"[{gameObject.name}] Cannot move, not on NavMesh! Resetting."); ResetState(); }
            }
            else if (currentAction.name != ChangeColorActionName) // Actions requiring target are null
            { Debug.LogError($"[{gameObject.name}] Target is null for required action '{currentAction.name}'! Resetting."); ResetState(); }
            else // Should not happen
            { Debug.LogError($"[{gameObject.name}] Unhandled action type '{currentAction.name}'! Resetting."); ResetState(); }
        }
        else
        {
            // Debug.LogWarning($"[{gameObject.name}] No suitable action found! Idling.");
            currentAction = null;
            currentState = SquimState.Idle_ChoosingAction;
            if (agent.isOnNavMesh && agent.enabled) agent.isStopped = true;
        }
    }

    float GetEffectiveActionDuration(Action action, ref GameObject foundTarget, Fish[] currentFish)
    {
        float travelTime = 0;
        foundTarget = null;

        switch (action.name)
        {
            case EatFishActionName:
                GameObject nearestFish = FindNearestFish(currentFish);
                if (nearestFish == null) return float.PositiveInfinity;
                foundTarget = nearestFish;
                travelTime = CalculatePathTime(nearestFish.transform.position);
                return travelTime == float.PositiveInfinity ? float.PositiveInfinity : (travelTime > 0 ? travelTime : 0.1f); // Instant on arrival

            case DrinkWaterActionName:
                if (waterCoolerWaypoint == null) return float.PositiveInfinity;
                foundTarget = waterCoolerWaypoint.gameObject;
                travelTime = CalculatePathTime(waterCoolerWaypoint.position);
                return travelTime == float.PositiveInfinity ? float.PositiveInfinity : travelTime + action.duration;

            case SleepInBedActionName:
                if (bedWaypoint == null) return float.PositiveInfinity;
                foundTarget = bedWaypoint.gameObject;
                travelTime = CalculatePathTime(bedWaypoint.position);
                return travelTime == float.PositiveInfinity ? float.PositiveInfinity : travelTime + action.duration;

            case ChangeColorActionName: return action.duration;
            default: return action.duration;
        }
    }

    float CalculatePathTime(Vector3 targetPosition)
    {
        if (!agent.isOnNavMesh || !agent.enabled) return float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();
        if (agent.CalculatePath(targetPosition, path) && (path.status == NavMeshPathStatus.PathComplete || path.status == NavMeshPathStatus.PathPartial) && agent.speed > 0)
        {
            if (path.corners.Length < 2) return 0f;
            float distance = 0f;
            for (int i = 0; i < path.corners.Length - 1; i++) distance += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            return distance / agent.speed;
        }
        return float.PositiveInfinity;
    }

    void HandleMovement()
    {
        if (currentAction == null) { ResetState(); return; } // Should not happen often

        if (currentAction.name == EatFishActionName)
        {
            if (targetObject == null && timeSinceLastFishCheck > 0) { ResetState(); return; } // Target destroyed

            timeSinceLastFishCheck += Time.deltaTime;
            if (timeSinceLastFishCheck >= fishCheckInterval)
            {
                timeSinceLastFishCheck = 0f;
                Fish[] currentFish = FindObjectsByType<Fish>(FindObjectsSortMode.None);
                GameObject nearestFish = FindNearestFish(currentFish);

                if (nearestFish == null) { ResetState(); return; } // No more fish

                if (nearestFish != targetObject || targetObject == null) // Switch or assign initial target
                {
                    targetObject = nearestFish;
                }
                // Always update destination if agent is active and target exists
                if (targetObject != null && agent.isOnNavMesh && agent.enabled) agent.SetDestination(targetObject.transform.position);
            }
            // Ensure moving if stopped unexpectedly
            if (agent.isOnNavMesh && agent.enabled && agent.isStopped && !agent.pathPending && targetObject != null)
            {
                if (agent.destination != targetObject.transform.position) agent.SetDestination(targetObject.transform.position);
                agent.isStopped = false;
            }
        }
        else // Bed or Water Cooler movement
        {
            if (targetObject == null) { Debug.LogError($"[{gameObject.name}] Waypoint target null for {currentAction.name}! Resetting."); ResetState(); return; }

            // Check bed occupancy dynamically
            if (currentAction.name == SleepInBedActionName && occupantOfBed != null && occupantOfBed != this.gameObject)
            {
                Debug.Log($"[{gameObject.name}] Bed occupied by {occupantOfBed.name}. Re-evaluating.");
                ResetState(); return;
            }

            bool startWaiting = false;
            float distanceToTarget = Vector3.Distance(transform.position, targetObject.transform.position);

            // Check if close enough to start the waiting action
            if (currentAction.name == DrinkWaterActionName)
            {
                // Use proximity threshold for water
                if (distanceToTarget <= WaterProximityThreshold)
                {
                    startWaiting = true;
                }
            }
            else if (currentAction.name == SleepInBedActionName)
            {
                // For bed, rely primarily on NavMeshAgent reaching the destination
                if (agent.isOnNavMesh && agent.enabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    startWaiting = true;
                }
                // Optional: Add a proximity check as a fallback for bed if needed
                // else if (distanceToTarget <= agent.stoppingDistance + 0.5f) startWaiting = true;
            }

            // If conditions met to start waiting:
            if (startWaiting)
            {
                currentState = SquimState.PerformingAction_Waiting;
                actionTimer = currentAction.duration;
                if (agent.isOnNavMesh && agent.enabled)
                {
                    agent.isStopped = true;
                    agent.ResetPath(); // Stop moving completely and clear path
                }
            }
            else // Not close enough or haven't reached destination yet, ensure movement continues
            {
                if (agent.isOnNavMesh && agent.enabled)
                {
                    // Update destination if it's wrong or agent isn't pathing towards it
                    if (!agent.hasPath || agent.pathPending || agent.destination != targetObject.transform.position)
                    {
                        agent.SetDestination(targetObject.transform.position);
                    }
                    // Ensure agent is not stopped if it should be moving
                    if (agent.isStopped)
                    {
                        agent.isStopped = false;
                    }
                }
                else { Debug.LogError($"[{gameObject.name}] Cannot move, not on NavMesh! Resetting."); ResetState(); }
            }
        }
    }

    void HandleWaitingAction()
    {
        actionTimer -= Time.deltaTime;
        if (actionTimer <= 0)
        {
            currentAction.Perform(goals);

            if (currentAction.name == ChangeColorActionName) PerformMaterialChange();
            else if (currentAction.name == SleepInBedActionName && occupantOfBed == this.gameObject)
            {
                occupantOfBed = null;
            }

            if (CheckDeathCondition()) return;

            ResetState();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (currentState == SquimState.MovingToTarget && currentAction?.name == EatFishActionName && targetObject != null && collision.gameObject == targetObject)
        {
            Debug.Log($"[{gameObject.name}] Ate fish '{targetObject.name}'.");
            currentAction.Perform(goals);
            // LogGoals(); // Optional: Log goals after eating
            GameObject eatenFish = targetObject;
            if (CheckDeathCondition())
            {
                // If died, ensure fish is still destroyed if needed
                if (eatenFish != null) Destroy(eatenFish);
                return; // Exit if died
            }
            ResetState(); // Reset before destroying fish
            if (eatenFish != null) Destroy(eatenFish);
        }
    }

    bool CheckDeathCondition()
    {
        foreach (var goal in goals)
        {
            if (goal.value >= 100)
            {
                Debug.Log($"{gameObject.name} died of {goal.name} ({goal.value:F0})!");
                if (uiManager != null)
                {
                    uiManager.ReportSquimDeath(gameObject.name, goal.name);
                }
                if (currentAction?.name == SleepInBedActionName && occupantOfBed == this.gameObject)
                {
                    occupantOfBed = null; // Make sure bed is freed if died while sleeping
                }
                Destroy(gameObject);
                return true; // Squim died
            }
        }
        return false;
    }

    void ResetState()
    {
        bool wasHandlingBed = currentAction?.name == SleepInBedActionName;
        if (wasHandlingBed && occupantOfBed == this.gameObject)
        {
            // Debug.Log($"[{gameObject.name}] Resetting state, freeing bed.");
            occupantOfBed = null;
        }
        currentState = SquimState.Idle_ChoosingAction;
        currentAction = null;
        targetObject = null;
        actionTimer = 0;
        timeSinceLastFishCheck = 0;
        if (agent.isOnNavMesh && agent.enabled) agent.isStopped = true; // agent.ResetPath();
    }

    float GetPredictedDiscontentment(Action action, float effectiveDuration)
    {
        if (effectiveDuration == float.PositiveInfinity) return float.PositiveInfinity;
        float discontentment = 0;
        float tickFactor = effectiveDuration / tickLength;

        foreach (var goal in goals)
        {
            float predictedValue = goal.value;
            if (action.effectsOnGoals.TryGetValue(goal, out float effectValue)) predictedValue += effectValue;
            if (tick.effectsOnGoals.TryGetValue(goal, out float tickEffectValue)) predictedValue += tickEffectValue * tickFactor;
            discontentment += goal.CalculateDiscontentment(Mathf.Max(0, predictedValue));
        }
        return discontentment;
    }

    GameObject FindNearestFish(Fish[] fishList)
    {
        GameObject nearest = null;
        float minDist = float.PositiveInfinity;
        Vector3 currentPos = transform.position;
        foreach (Fish fish in fishList)
        {
            if (fish == null) continue;
            float distSqr = (fish.transform.position - currentPos).sqrMagnitude;
            if (distSqr < minDist) { minDist = distSqr; nearest = fish.gameObject; }
        }
        return nearest;
    }

    void PerformMaterialChange()
    {
        Transform squimBodyTransform = transform.Find("Squim/SquimBody");
        if (squimBodyTransform == null)
        {
            Debug.LogError($"[{gameObject.name}] Could not find 'Squim/SquimBody' child transform.");
            return;
        }

        Renderer[] bodyRenderers = squimBodyTransform.GetComponentsInChildren<Renderer>();
        if (bodyRenderers == null || bodyRenderers.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] No renderers found under 'SquimBody'.");
            return;
        }

        if (availableMaterials == null || availableMaterials.Count <= 1)
        {
            return; // Cannot change material if there are 0 or 1 options
        }

        Material currentMaterial = bodyRenderers[0].sharedMaterial;

        List<Material> possibleNewMaterials = new List<Material>(availableMaterials);
        possibleNewMaterials.Remove(currentMaterial);

        if (possibleNewMaterials.Count == 0)
        {
            if (availableMaterials.Count > 0)
                possibleNewMaterials.Add(availableMaterials[0]);
            else
                return;
        }

        Material newMaterial = possibleNewMaterials[Random.Range(0, possibleNewMaterials.Count)];

        foreach (Renderer rend in bodyRenderers)
        {
            rend.material = newMaterial;
        }
    }

    float GetTotalDiscontentment() => goals.Sum(goal => goal.GetDiscontentment());

    public string GetStatusString()
    {
        string status = $"{gameObject.name}\n";
        foreach (var goal in goals) status += $"{goal.name}: {goal.value:F0}\n";
        status += $"Discontentment: {GetTotalDiscontentment():F0}\n";

        if (currentAction != null)
        {
            status += $"Action: {currentAction.name}\n";
            if (currentAction.name == EatFishActionName && currentState == SquimState.MovingToTarget && targetObject != null)
            {
                status += $"(-> fish)\n";
            }
            if (currentAction.name == DrinkWaterActionName && currentState == SquimState.MovingToTarget && targetObject != null)
            {
                status += $"(-> water cooler)\n";
            }
            if (currentAction.name == SleepInBedActionName && currentState == SquimState.MovingToTarget && targetObject != null)
            {
                status += $"(-> bed)\n";
            }
            else if (currentState == SquimState.PerformingAction_Waiting) status += $"({actionTimer:F1}s)\n";
            else if (currentAction.name == EatFishActionName && targetObject == null) status += "Seeking Fish...\n";
            else status += "\n";
        }
        else { status += "Action: None\n"; }

        return status;
    }

    void OnDestroy()
    {
        if (occupantOfBed == this.gameObject)
        {
            occupantOfBed = null;
        }
        CancelInvoke("ApplyTick"); // Stop the tick invoke if it's running
    }
}