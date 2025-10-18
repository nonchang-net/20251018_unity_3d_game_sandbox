using UnityEngine;

public class StaticMessenger : MonoBehaviour
{
    [Header("表示内容")]
    [Tooltip("表示するテキスト")]
    [SerializeField] private string message;

    public string Message => message;
}
