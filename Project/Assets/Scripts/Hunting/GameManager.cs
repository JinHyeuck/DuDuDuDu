using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private bool isGameOver = false;

    void Awake() { Instance = this; }

    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        Debug.Log("Game Over!");
    }
}
