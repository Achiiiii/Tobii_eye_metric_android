using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PointButtonTrigger : MonoBehaviour
{
    Button btn;
    Transform transform;
    RectTransform rect;
    Image image;
    public PointController pointController;
    public AudioSource audioSource;
    public Sprite originSprite;
    public Sprite triggerSprite;
    public TMPro.TMP_Text childrenText;
    private bool _isEnter = false;
    private bool _isScaleReset = true;
    private int _index;
    private bool _isTriggered = false;

    void Start()
    {
        _index = int.Parse(gameObject.name);
        rect = gameObject.GetComponent<RectTransform>();
        btn = gameObject.GetComponent<Button>();
        transform = gameObject.GetComponent<Transform>();
        image = gameObject.GetComponent<Image>();
        childrenText.text = gameObject.name;
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
        if (_isTriggered) return;
        _isEnter = true;
        if (pointController.curIndex + 1 == _index || pointController.curIndex == _index)
        {
            if (_isScaleReset)
            {
                _isScaleReset = false;
                transform.DOScale(1.2f, 1).SetEase(Ease.OutCubic).OnComplete(() =>
                {
                    transform.DOScale(1, 0);
                    btn.onClick.Invoke();
                    _isScaleReset = true;
                });
            }
        }
        else
        {
            image.DOFade(0.2f, 1);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_isTriggered) return;
        _isEnter = false;
        image.DOKill();
        image.DOFade(1, 0);
        if (pointController.curIndex + 1 == _index || pointController.curIndex == _index)
        {
            StartCoroutine(DelayExit());
        }
    }

    private IEnumerator DelayExit()
    {
        yield return new WaitForSeconds(0.1f);
        if (!_isEnter)
        {
            transform.DOKill();
            transform.DOScale(1, 0);
            _isScaleReset = true;
        }
    }
    private void ClickAudio()
    {
        if (audioSource)
            audioSource.Play();
    }
    public void TriggerPoint()
    {
        pointController.SetPoint(gameObject.transform.localPosition, _index);
        image.sprite = triggerSprite;
        _isTriggered = true;
    }
    public void Reset()
    {
        if (image)
        {
            image.sprite = originSprite;
            image.DOKill();
            image.DOFade(1, 0);
        }
        if (transform)
        {
            transform.DOKill();
            transform.DOScale(1, 0);
        }
        _isEnter = false;
        _isScaleReset = true;
        _isTriggered = false;
    }
}
