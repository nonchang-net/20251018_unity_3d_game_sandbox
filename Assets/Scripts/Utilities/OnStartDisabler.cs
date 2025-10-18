using UnityEngine;

public class OnStartDisabler : MonoBehaviour
{
    void Start()
    {
        gameObject.SetActive(false);
    }
}
