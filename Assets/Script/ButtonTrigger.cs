using UnityEngine;
using UnityEngine.UI;

public class ButtonTrigger : MonoBehaviour
{
    // After any selection every dwell button pauses briefly, so a gaze left resting on an option
    // (e.g. when the next question appears) starts a fresh dwell instead of firing immediately.
    private const float SelectionCooldownSeconds = 1f;
    private static float s_cooldownUntil;

    Button btn;
    RectTransform rect;
    public AudioSource audioSource;
    private bool _isEnter = false;
    private float _dwellTime = 0f;
    private float _exitTime = 0f;
    private readonly float _triggerTime = 1.5f;
    // Brief gaze jitter outside the button should not throw away dwell progress.
    private readonly float _exitGraceTime = 0.3f;

    void Start()
    {
        rect = gameObject.GetComponent<RectTransform>();
        btn = gameObject.GetComponent<Button>();
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

    void Update()
    {
        if (_isEnter)
        {
            if (Time.time < s_cooldownUntil || !btn.IsInteractable())
            {
                if (_dwellTime > 0f)
                    ResetDwell();
                return;
            }

            _exitTime = 0f;
            _dwellTime += Time.deltaTime;
            GazeDwellIndicator.Report(this, _dwellTime / _triggerTime);
            if (_dwellTime >= _triggerTime)
                Trigger();
        }
        else if (_dwellTime > 0f)
        {
            _exitTime += Time.deltaTime;
            if (_exitTime >= _exitGraceTime)
                ResetDwell();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        _isEnter = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _isEnter = false;
    }

    private void Trigger()
    {
        s_cooldownUntil = Time.time + SelectionCooldownSeconds;
        _dwellTime = 0f;
        _exitTime = 0f;
        GazeDwellIndicator.Complete();
        btn.onClick.Invoke();
    }

    private void ResetDwell()
    {
        _dwellTime = 0f;
        _exitTime = 0f;
        GazeDwellIndicator.Release(this);
    }

    private void ClickAudio()
    {
        if (audioSource)
            audioSource.Play();
    }

    void OnDisable()
    {
        _isEnter = false;
        ResetDwell();
    }
}
