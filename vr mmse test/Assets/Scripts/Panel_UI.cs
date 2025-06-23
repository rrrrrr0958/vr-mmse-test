using UnityEngine;
using UnityEngine.SceneManagement;

public class NewEmptyCSharpScript
{
    public void OnRestartPress()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
