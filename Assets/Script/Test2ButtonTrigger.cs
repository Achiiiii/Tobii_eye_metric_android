using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class Test2ButtonTrigger : MonoBehaviour
{
    Button btn;
    Transform transform;
    RectTransform rect;
    public AudioSource audioSource;
    private bool _isEnter;
    private bool _isScaleReset = true;

    void Start()
    {
        rect = gameObject.GetComponent<RectTransform>();
        btn = gameObject.GetComponent<Button>();
        transform = gameObject.GetComponent<Transform>();
        BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
        float pivotX = rect.pivot.x;
        float pivotY = rect.pivot.y;
        float offsetX = 0;
        float offsetY = 0;
        switch (pivotX)
        {
            case 0:
                offsetX = rect.sizeDelta.x / 2;
                break;
            case 0.5f:
                offsetX = 0;
                break;
            case 1:
                offsetX = rect.sizeDelta.x / 2 * -1;
                break;
        }
        switch (pivotY)
        {
            case 0:
                offsetY = rect.sizeDelta.y / 2;
                break;
            case 0.5f:
                offsetY = 0;
                break;
            case 1:
                offsetY = rect.sizeDelta.y / 2 * -1;
                break;
        }
        boxCollider.offset = new Vector2(offsetX, offsetY);
        boxCollider.isTrigger = true;
        boxCollider.size = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y);
        btn.onClick.AddListener(ClickAudio);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        _isEnter = true;
        // if (_isScaleReset)
        // {
        //     _isScaleReset = false;
        //     transform.DOScale(1.2f, 3).SetEase(Ease.OutCubic).OnComplete(() =>
        //     {
        //         transform.DOScale(1, 0);
        //         btn.onClick.Invoke();
        //         _isScaleReset = true;
        //     });
        // }
        btn.onClick.Invoke();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _isEnter = false;
        // StartCoroutine(DelayExit());
    }

    // private IEnumerator DelayExit()
    // {
    //     yield return new WaitForSeconds(0.2f);
    //     if (!_isEnter)
    //     {
    //         transform.DOKill();
    //         transform.DOScale(1, 0);
    //         _isScaleReset = true;
    //     }
    // }
    private void ClickAudio()
    {
        if (audioSource)
            audioSource.Play();
    }
}
