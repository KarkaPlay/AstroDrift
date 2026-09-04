using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

/// <summary>
/// Управление кораблём (GDD §2): тап/удержание левой половины = против часовой,
/// правой = по часовой, обе одновременно = прямо. Угловая скорость фиксированная,
/// инерция нулевая. Rigidbody2D Kinematic, движение в FixedUpdate.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class ShipController : MonoBehaviour
{
    [SerializeField] private GameConfig config;

    private Rigidbody2D _rb;
    private CircleCollider2D _collider;
    private MeshRenderer _renderer;
    private float _angularSpeed;
    private float _speed;
    private float _targetSpeed;   // целевая скорость (DifficultyManager пишет сюда; факт — после разгона)
    private float _accelDur;      // длительность разгона 0 → target (0 = сразу полная скорость)
    private float _accelT;        // прошло с начала разгона
    private bool _invulnerable;
    private float _invulnTimer;
    private bool _dead;

    public bool IsDead => _dead;

    /// <summary>Ссылка на сущность корабля в игровом мире (синглтон-доступ для спавнеров).</summary>
    public static ShipController Instance { get; private set; }

    private void Awake()
    {
        // Проект использует Input System (new) only: legacy Input.touchCount не работает в билде.
        EnhancedTouchSupport.Enable();
        Instance = this;
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<CircleCollider2D>();
        _renderer = GetComponent<MeshRenderer>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _collider.isTrigger = true;
        _collider.radius = config != null ? config.shipRadius : 0.25f;
        _angularSpeed = config != null ? config.shipAngularSpeed : 180f;
        _speed = config != null ? config.shipSpeed : 5f;

        // ux4-4: TrailRenderer.autodestruct убивал дочерний Trail-объект после смерти
        // (трейл дорисовывался → Destroy) — после рестарта след пропадал навсегда.
        foreach (var trail in GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.autodestruct = false;
            trail.emitting = false;
        }
    }

    /// <summary>Инициализация, когда конфиг назначается кодом (Bootstrap).</summary>
    public void InitFrom(GameConfig cfg)
    {
        config = cfg;
        _angularSpeed = cfg.shipAngularSpeed;
        _speed = cfg.shipSpeed;
        _collider.radius = cfg.shipRadius;
    }

    /// <param name="accelTime">Разгон 0 → speed за accelTime (ease-out). 0 = полная скорость сразу
    /// (меню/рестарт/continue). Первый старт: GameConfig.startAccelerateTime.</param>
    public void BeginRun(Vector3 startPos, float speed, float accelTime = 0f)
    {
        _targetSpeed = speed;
        _speed = accelTime > 0f ? 0f : speed;
        _accelDur = Mathf.Max(0f, accelTime);
        _accelT = 0f;
        transform.position = startPos;
        transform.rotation = Quaternion.identity; // нос вверх
        _dead = false;
        // ux4-R2 (фикс №1): страховка от остаточной инерции — кинематическое тело
        // движется по velocity; после смерти/дрейфа в меню оно обязано стоять.
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        // Баг «след исчезает после рестарта»: Kill() отключал TrailRenderer и
        // коллайдер, BeginRun их не возвращал; autodestruct (ux4-4) к тому же
        // уничтожал сам Trail-объект. Восстанавливаем всё явно + очищаем
        // Points-очередь, чтобы не тянулись линии от старых (до-смертных) позиций.
        // Trail — ДОЧЕРНИЙ объект корабля: ищем через GetComponentsInChildren(true).
        foreach (var trail in GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.gameObject.SetActive(true);
            trail.enabled = true;
            trail.Clear();
            trail.emitting = true;
        }
        _collider.enabled = true;
        _renderer.enabled = true;

        // Стартовая неуязвимость с миганием убрана (решение владельца: корабль не мигает
        // при старте/рестарте). Мигает ТОЛЬКО неуязвимость после continue — её ставит
        // GameManager.ContinueRun отдельным вызовом SetInvulnerable.
        _invulnerable = false;
        _invulnTimer = 0f;
    }

    public void SetSpeed(float speed) => _targetSpeed = speed;

    public void SetInvulnerable(float duration)
    {
        _invulnerable = true;
        _invulnTimer = duration;
    }

    public void Kill()
    {
        _dead = true;
        // ux4-R2 (фикс №1, КРИТИЧНО): после Kill() FixedUpdate перестаёт писать
        // velocity, а кинематическое тело продолжает лететь по последнему заданному
        // вектору — отсюда дрейф после смерти (и «вынос» корабля после Retry/Home).
        // Полная остановка в момент смерти: velocity/angularVelocity = 0.
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _renderer.enabled = false;
        _collider.enabled = false;
        // Trail — ДОЧЕРНИЙ объект корабля: гасим все трейлы в детях (включая неактивные)
        foreach (var trail in GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.emitting = false;
            trail.enabled = false;
            trail.Clear();
        }
    }

    private void FixedUpdate()
    {
        // InputEnabled: управление с t=0 при первом старте (корабль уже в разгоне),
        // t=0.5 s при рестарте/continue — до этого камера в хореографическом полёте.
        if (_dead || GameManager.Instance == null || GameManager.Instance.State != GameState.Playing
            || !GameManager.Instance.InputEnabled)
            return;

        // Разгон старта (§5): 0 → targetSpeed за _accelDur по ease-out quad — быстрый
        // отклик с первого кадра (движение видно сразу), к цели подъезжает плавно.
        if (_accelT < _accelDur)
        {
            _accelT += Time.fixedDeltaTime;
            float k = Mathf.Clamp01(_accelT / _accelDur);
            _speed = _targetSpeed * (1f - (1f - k) * (1f - k));
        }
        else
        {
            _speed = _targetSpeed;
        }

        // Ввод: 0 = прямо, +1 = влево (против ЧС), -1 = вправо (по ЧС). Обе стороны = прямо.
        // EnhancedTouch — API нового Input System, работает на мобильных при activeInputHandler = Input System.
        int dir = 0;
        bool left = false, right = false;
        if (EnhancedTouchSupport.enabled)
        {
            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began ||
                    touch.phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                    touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    if (touch.screenPosition.x < Screen.width * 0.5f) left = true;
                    else right = true;
                }
            }
        }
        // ux4-1: в Editor мыши нет среди activeTouches — зажатая ЛКМ работает как тач
        // (левая/правая половина экрана). Тач + мышь не конфликтуют: на устройстве мыши нет.
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            if (mouse.position.ReadValue().x < Screen.width * 0.5f) left = true;
            else right = true;
        }
        dir = left != right ? (left ? 1 : -1) : 0;
        // Fallback для клавиатуры (editor/десктоп-тесты)
        var kb = Keyboard.current;
        if (dir == 0 && kb != null)
        {
            if (kb.aKey.isPressed) dir = 1;
            else if (kb.dKey.isPressed) dir = -1;
            else if (kb.leftArrowKey.isPressed) dir = 1;
            else if (kb.rightArrowKey.isPressed) dir = -1;
        }

        _rb.rotation += dir * _angularSpeed * Time.fixedDeltaTime;
        Vector2 forward = transform.up;
        _rb.linearVelocity = forward * _speed;
    }

    private void Update()
    {
        if (_invulnerable)
        {
            _invulnTimer -= Time.deltaTime;
            float blink = Mathf.Sin(Time.time * 30f) > 0f ? 1f : 0.15f;
            _renderer.enabled = blink > 0.5f;
            if (_invulnTimer <= 0f)
            {
                _invulnerable = false;
                _renderer.enabled = true;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_dead || _invulnerable) return;
        // Компонентная проверка вместо тега (надёжнее: тег может потеряться — баг 3).
        var ast = other.GetComponentInParent<Asteroid>();
        var missile = other.GetComponentInParent<Missile>();
        if (ast != null || missile != null)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnShipHit(this, other);
        }
    }
}
