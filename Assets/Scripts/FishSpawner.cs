using UnityEngine;

public class FishSpawner : MonoBehaviour
{
    public GameObject fishPrefab;
    public float spawnInterval = 10f;
    public float spawnRadius = 15f;
    public int maxFish = 10;

    void Start()
    {
        InvokeRepeating("SpawnFish", spawnInterval, spawnInterval);
    }

    void SpawnFish()
    {
        if (FindObjectsByType<Fish>(FindObjectsSortMode.None).Length >= maxFish)
        {
            return;
        }

        // Find a random position within the radius on the NavMesh
        Vector3 randomPos = Fish.RandomNavSphere(transform.position, spawnRadius, -1);

        if (randomPos != Vector3.zero)
        {
            Instantiate(fishPrefab, randomPos, Quaternion.identity);
        }
    }
}