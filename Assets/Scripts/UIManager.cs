using UnityEngine;
using UnityEngine.UI; // Required for Button
using TMPro; // Required for TextMeshProUGUI
using UnityEngine.EventSystems; // Required for checking UI clicks
using UnityEngine.AI;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI statusDisplay; // Assign your UI Text element here in the Inspector
    public Camera mainCamera; // Assign your main camera (or leave null to use Camera.main)
    public Button spawnButton; // Assign your UI Button element here
    public GameObject squimPrefab; // Assign the Squim prefab here

    private Squim selectedSquim = null;
    private string defaultText = "Click on a Squim \nto view its status\n";
    private string deathMessage = null; // Stores the last death message
    private int nextSquimIndex = 2; // Start naming spawned Squims from 2

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        if (mainCamera == null)
        {
            Debug.LogError("UIManager: Main Camera not found!");
            enabled = false; // Disable script if no camera
            return;
        }
        if (statusDisplay == null)
        {
            Debug.LogError("UIManager: Status Display TextMeshProUGUI not assigned!");
            enabled = false; // Disable script if no display assigned
            return;
        }
        if (spawnButton == null)
        {
            Debug.LogError("UIManager: Spawn Button not assigned!");
            enabled = false;
            return;
        }
        if (squimPrefab == null)
        {
            Debug.LogError("UIManager: Squim Prefab not assigned!");
            enabled = false;
            return;
        }

        statusDisplay.text = defaultText; // Initial text
    }

    void Update()
    {
        // --- Input Handling ---
        if (Input.GetMouseButtonDown(0)) // Ignore UI clicks
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Squim squim = hit.collider.GetComponent<Squim>();
                if (squim != null)
                {
                    selectedSquim = squim;
                    deathMessage = null; // Clear death message on successful selection
                }
            }
        }

        // --- Display Update ---
        if (deathMessage != null)
        {
            // A death message is active, display it
            if (statusDisplay.text != deathMessage)
                statusDisplay.text = deathMessage;
        }
        else if (selectedSquim != null)
        {
            // Check if selected Squim still exists before getting status
            if (selectedSquim == null)
            {
                // Squim was destroyed after selection but before update, show default
                if (statusDisplay.text != defaultText) statusDisplay.text = defaultText;
            }
            else
            {
                // Display selected Squim's status
                statusDisplay.text = selectedSquim.GetStatusString();
            }

        }
        else
        {
            // No death message, no selected Squim, show default
            if (statusDisplay.text != defaultText)
                statusDisplay.text = defaultText;
        }
    }

    public void SpawnNewSquim()
    {
        if (squimPrefab == null)
        {
            Debug.LogError("Cannot spawn Squim, prefab is not assigned!");
            return;
        }

        // Make sure the spawn position is on the NavMesh
        Vector3 spawnPos = new Vector3(0, 1, 0);
        NavMeshHit hit;
        // Sample slightly above the desired point to ensure it finds the ground NavMesh
        if (NavMesh.SamplePosition(spawnPos + Vector3.up, out hit, 2.0f, NavMesh.AllAreas))
        {
            spawnPos = hit.position; // Use the valid NavMesh position
        }
        else
        {
            Debug.LogWarning($"Could not find valid NavMesh position near (0,1,0) to spawn Squim. Spawning at exact point.");
            // Optionally, you could choose not to spawn if no valid point is found
            // return;
        }


        GameObject newSquimObj = Instantiate(squimPrefab, spawnPos, Quaternion.identity);
        newSquimObj.name = "Squim " + nextSquimIndex;
        Debug.Log($"Spawned {newSquimObj.name} at {spawnPos}");
        nextSquimIndex++;
    }

    // Called by Squim when it dies
    public void ReportSquimDeath(string squimName, string causeOfDeath)
    {
        deathMessage = $"{squimName} died of {causeOfDeath}!";
        if (selectedSquim != null && selectedSquim.name == squimName)
        {
            selectedSquim = null; // Deselect the Squim that just died
        }
        // Update the display immediately
        if (statusDisplay != null) statusDisplay.text = deathMessage;
    }
}