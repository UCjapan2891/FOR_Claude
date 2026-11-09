using TMPro;
using UnityEngine;

namespace VRClass.Lesson
{
    /// <summary>受講者への案内（「HMD を外してください」など）を World Space の Canvas に表示する。</summary>
    public sealed class LessonMessageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private GameObject root;

        public void Show(string message)
        {
            if (text != null)
            {
                text.text = message;
            }

            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            GameObject target = root != null ? root : gameObject;
            target.SetActive(visible);
        }
    }
}
