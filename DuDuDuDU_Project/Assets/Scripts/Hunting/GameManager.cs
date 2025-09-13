using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private bool isGameOver = false;

    public int WallHp;

    public Wall wall;

    void Awake() { Instance = this; }

    private void Start()
    {
        wall.SetInit(WallHp);
    }

    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        Debug.Log("Game Over!");
    }
}
