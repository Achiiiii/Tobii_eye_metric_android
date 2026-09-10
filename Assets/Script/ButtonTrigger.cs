using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ButtonTrigger : MonoBehaviour
{
    Button btn;
    Transform transform;
    RectTransform rect;
    public AudioSource audioSource;
    private bool _isEnter = false;
    private bool _isScaleReset = true;
    private float _delayTriggerTime = 0.1f;
    private float _triggerTime = 1.5f;
    private Image _childImage;
    private Color _childImageColor;
    private readonly Color _enterColor = new Color32(0x02, 0xDF, 0x82, 0xFF);

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
        Transform imageChild = transform.Find("Image");
        if (imageChild)
        {
            _childImage = imageChild.GetComponent<Image>();
            if (_childImage)
                _childImageColor = _childImage.color;
        }
        btn.onClick.AddListener(ClickAudio);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        _isEnter = true;
        if (_isScaleReset)
        {
            _isScaleReset = false;
            if (_childImage)
                _childImage.DOColor(_enterColor, _triggerTime).SetEase(Ease.OutCubic);
            transform.DOScale(1.2f, _triggerTime).SetEase(Ease.OutCubic).OnComplete(() =>
            {
                transform.DOScale(1, 0);
                ResetChildImageColor();
                btn.onClick.Invoke();
                _isScaleReset = true;
            });
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _isEnter = false;
        StartCoroutine(DelayExit());
    }

    private IEnumerator DelayExit()
    {
        yield return new WaitForSeconds(_delayTriggerTime);
        if (!_isEnter)
        {
            transform.DOKill();
            transform.DOScale(1, 0);
            ResetChildImageColor();
            _isScaleReset = true;
        }
    }

    private void ResetChildImageColor()
    {
        if (!_childImage)
            return;
        _childImage.DOKill();
        _childImage.color = _childImageColor;
    }
    private void ClickAudio()
    {
        if (audioSource)
            audioSource.Play();
    }
    void OnDisable()
    {
        if (transform)
        {
            transform.DOKill();
            transform.DOScale(1, 0);
        }
        ResetChildImageColor();
        _isEnter = false;
        _isScaleReset = true;
    }
}
