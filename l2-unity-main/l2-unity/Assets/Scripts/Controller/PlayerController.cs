using UnityEngine;

public class PlayerController : MonoBehaviour
{
    /* Components */
    private CharacterController _controller;
    /*Rotate*/
    private float _finalAngle;

    /* Movement */
    [SerializeField] private Vector3 _moveDirection;
    [SerializeField] private float _currentSpeed;
    [SerializeField] private float _defaultRunSpeed = 4;
    [SerializeField] private float _defaultWalkSpeed = 4;
    [SerializeField] private float _defaultSwimSpeed = 4;
    [SerializeField] private bool _running = true;
    [SerializeField] private bool _swimming = false;
    [SerializeField] private bool _jumping = true;
    [SerializeField] private float _measuredSpeed;
    private Vector3 _currentPos;
    private Vector3 _lastPos;
    private Vector2 _axis;

    /* Gravity */
    public float _verticalVelocity = 0;
    [SerializeField] private float _jumpForce = 10;
    [SerializeField] private float _gravity = 28;
    // Encore trop rapide a 1.5 au test - redescendue nettement (1.5 -> 0.6).
    [SerializeField] private float _swimVerticalSpeed = 0.6f;
    // L'origine du transform est aux pieds du personnage (pas au centre) : si
    // on plafonne les pieds pile a la hauteur de surface, tout le corps se
    // retrouve au-dessus de l'eau au lieu d'etre a moitie submerge. On vise
    // donc les pieds a "surface - CollisionHeight * ratio" (0.7 = 70% du
    // corps sous l'eau, la tete depassant), plutot que la surface elle-meme.
    // Un poil trop haut a 0.7 au test - remontee legerement (0.7 -> 0.8).
    [SerializeField] private float _swimSubmergeRatio = 0.8f;

    /* Target */
    [SerializeField] private Vector3 _targetPosition;
    [SerializeField] private bool _runningToDestination = false;
    [SerializeField] private bool _intentionToRun = false;
    [SerializeField] private Transform _lookAtTarget;
    [SerializeField] private Transform _model;
    private float _stopAtRange;
    private Vector3 _flatTransformPos;
    private Camera _mainCamera;

    public float CurrentSpeed { get { return _currentSpeed; } }
    public float DefaultRunSpeed { get { return _defaultRunSpeed; } set { _defaultRunSpeed = value; } }
    public float DefaultWalkSpeed { get { return _defaultWalkSpeed; } set { _defaultWalkSpeed = value; } }
    public float DefaultSwimSpeed { get { return _defaultSwimSpeed; } set { _defaultSwimSpeed = value; } }
    public bool RunningToDestination { get { return _runningToDestination; } }
    public bool IntentionToRun { get { return _intentionToRun; } set { _intentionToRun = value; } }
    public bool Running { get { return _running; } set { _running = value; } }
    public bool Swimming { get { return _swimming; } set { _swimming = value; } }
    public bool Jumping { get { return _jumping; } set { _jumping = value; } }
    public Vector3 MoveDirection { get { return _moveDirection; } }

    private static PlayerController _instance;
    public static PlayerController Instance { get { return _instance; } }

    public void Initialize()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    void OnDestroy()
    {
        _instance = null;
    }

    void Start()
    {
        if (_model == null)
        {
            _model = transform.GetChild(0);
        }

        _controller = GetComponent<CharacterController>();
        _mainCamera = CameraController.Instance.GetComponent<Camera>();
    }

    void Update()
    {
        _flatTransformPos = new Vector3(transform.position.x, 0, transform.position.z);

        if (InputManager.Instance.Move)
        {
            ResetDestination(false);
        }

        if (_runningToDestination)
        {
            if (ShouldRunToDestination(_stopAtRange))
            {
                MoveToTargetPosition();
            }
        }
        else if (PlayerStateMachine.Instance.CanMove())
        {
            ListenToInputs();
        }

        if (_lookAtTarget != null)
        {
            UpdateFinalAngleToLookAt(_lookAtTarget);
        }

        if (PlayerStateMachine.Instance.CanMove() || PlayerStateMachine.Instance.State == PlayerState.ATTACKING || PlayerStateMachine.Instance.State == PlayerState.SKILL)
            _model.rotation = Quaternion.Lerp(_model.rotation, Quaternion.Euler(Vector3.up * _finalAngle), Time.deltaTime * 7.5f);


        if (PlayerStateMachine.Instance.CanMove())
        {
            _moveDirection = ApplyGravity(_moveDirection);
            _controller.Move(_moveDirection * Time.deltaTime);
        }
        else
        {
            _controller.Move(ApplyGravity(Vector3.zero) * Time.deltaTime);
        }

        MeasureSpeed();
    }

    private bool ShouldRunToDestination(float stopAtRange)
    {
        return Vector3.Distance(_flatTransformPos, _targetPosition) > stopAtRange;
    }

    public void SetDestination(Vector3 position, float distance)
    {
        // Debug.LogWarning($"Set destination: {position}");
        _intentionToRun = true;
        _runningToDestination = true;
        _stopAtRange = distance;
        _targetPosition = VectorUtils.To2D(position);
    }

    public void ResetDestination(bool targetReached)
    {
        _intentionToRun = false;
        _runningToDestination = false;
        _targetPosition = _flatTransformPos;
        ClickManager.Instance.HideLocator(targetReached);
    }

    public void ListenToInputs()
    {
        /* Update input axis */
        _axis = GetAxis();

        /* Speed */
        _currentSpeed = GetInputMoveSpeed(_currentSpeed);

        /* Angle */
        _finalAngle = GetInputRotationValue(_finalAngle);

        /* Direction */
        _moveDirection = GetInputDirection(_currentSpeed);
    }

    private void MeasureSpeed()
    {
        _currentPos = transform.position;
        _measuredSpeed = (_currentPos - _lastPos).magnitude / Time.deltaTime;
        _lastPos = _currentPos;
    }

    private void MoveToTargetPosition()
    {
        Vector3 relativeDirection = _targetPosition - _flatTransformPos;


        Vector3 relativeAxis = new Vector2(relativeDirection.x, relativeDirection.z);

        // Use Atan2 to calculate the angle in radians
        float angleInRadians = Mathf.Atan2(relativeDirection.x, relativeDirection.z);

        // Convert radians to degrees and adjust for Unity's coordinate system
        float angleInDegrees = Mathf.Rad2Deg * angleInRadians;

        // Ensure the angle is between 0 and 360 degrees
        angleInDegrees = (angleInDegrees + 360) % 360;

        _axis = relativeAxis;
        _finalAngle = angleInDegrees;

        if (_swimming)
        {
            _currentSpeed = _defaultSwimSpeed;
        }
        else if (_running)
        {
            _currentSpeed = _defaultRunSpeed;
        }
        else
        {
            _currentSpeed = _defaultWalkSpeed;
        }

        _moveDirection = relativeDirection.normalized * _currentSpeed;
    }

    public Vector2 GetAxis()
    {
        Vector2 localAxis;
        if (InputManager.Instance.MoveForward)
        {
            LookForward(true);
            localAxis = Vector2.up;
        }
        else
        {
            localAxis = Vector2.zero;
        }
        localAxis = localAxis + InputManager.Instance.MoveInput;
        localAxis = new Vector2(Mathf.Clamp(localAxis.x, -1f, 1f), Mathf.Clamp(localAxis.y, -1f, 1f));

        return localAxis;
    }

    private float GetInputRotationValue(float angle)
    {
        if (InputManager.Instance.Move)
        {
            angle = Mathf.Atan2(_axis.x, _axis.y) * Mathf.Rad2Deg;
            angle = Mathf.Round(angle / 45f);
            angle *= 45f;
            angle += _mainCamera.transform.eulerAngles.y;
        }

        return angle;
    }

    private Vector3 GetInputDirection(float speed)
    {
        /* Handle input direction */
        // Toujours relatif a la camera (comme la marche), plus de branche
        // "en l'air" separee : cette branche figeait la direction sur
        // transform.forward (sans lien avec la camera/les touches) des que
        // _controller.isGrounded etait faux - donc pas seulement en nageant
        // (deja corrige) mais aussi en sautant, ce qui donnait l'impression
        // que le personnage etait devie dans une direction fixe pendant tout
        // le saut, quel que soit l'axe presse.
        Vector3 rotationAxis = Vector3.up;
        Quaternion rotation = Quaternion.AngleAxis(_mainCamera.transform.eulerAngles.y, rotationAxis);
        Vector3 forward = rotation * Vector3.forward;
        Vector3 right = new Vector3(forward.z, 0, -forward.x);
        forward.y = 0;
        Vector3 direction = _axis.x * right + _axis.y * forward;

        direction = direction.normalized * speed;

        return direction;
    }

    private Vector3 ApplyGravity(Vector3 dir)
    {
        // En nage : pas de gravite, deplacement vertical pilote par le joueur
        // (SwimUp/SwimDown), borne en haut par la surface de l'eau (le fond
        // est deja gere naturellement par la collision terrain du
        // CharacterController, pas besoin de le clamper ici).
        if (_swimming)
        {
            dir.y = ApplySwimVertical();
            return dir;
        }

        /* Handle gravity */
        if (_controller.isGrounded)
        {
            if (_verticalVelocity < -1.25f)
            {
                _verticalVelocity = -1.25f;
            }
        }
        else
        {
            _verticalVelocity -= _gravity * Time.deltaTime;
        }
        dir.y = _verticalVelocity;

        return dir;
    }

    private float ApplySwimVertical()
    {
        float vertical = 0f;
        if (InputManager.Instance.SwimUp)
        {
            vertical = _swimVerticalSpeed;
        }
        else if (InputManager.Instance.SwimDown)
        {
            vertical = -_swimVerticalSpeed;
        }

        // Empeche de sortir de l'eau par le haut : si on est deja a/au-dessus
        // de la surface, ou si la vitesse demandee franchirait la surface
        // cette frame, on plafonne au lieu de couper brutalement a 0 (evite
        // un a-coup visible pile a la surface). Si la hauteur de surface est
        // introuvable (hors de l'emprise de tout plan d'eau connu), on NE
        // laisse PAS monter sans limite - mieux vaut bloquer prematurement a
        // un endroit precis que de laisser le joueur s'envoler hors de la
        // zone d'eau (ce qui faisait ensuite couper l'etat de nage cote
        // serveur en sortant du cuboide, rendant Z sans effet juste apres).
        if (vertical > 0f)
        {
            if (WaterSurfaceQuery.TryGetSurfaceHeight(transform.position, out float surfaceY))
            {
                // _controller.height (capsule reellement utilisee par la
                // collision) plutot que Appearance.CollisionHeight : source
                // fiable a coup sur (toujours configuree pour que la
                // collision fonctionne), contrairement a la donnee
                // "Appearance" dont on n'est pas certain qu'elle soit deja
                // peuplee au moment de ce calcul - c'etait trop haut au test
                // malgre ce plafond, signe qu'elle valait sans doute ~0 la.
                float feetTargetY = surfaceY - _controller.height * _swimSubmergeRatio;
                float distanceToTarget = feetTargetY - transform.position.y;
                vertical = distanceToTarget <= 0f ? 0f : Mathf.Min(vertical, distanceToTarget / Time.deltaTime);
            }
            else
            {
                vertical = 0f;
            }
        }

        _verticalVelocity = vertical;
        return vertical;
    }

    private float GetInputMoveSpeed(float speed)
    {
        float smoothDuration = 0.2f;


        float selectSpeed = Swimming ? _defaultSwimSpeed : (Running ? _defaultRunSpeed : _defaultWalkSpeed);

        if (InputManager.Instance.Move)
        {
            speed = selectSpeed;
        }
        else if (speed > 0 && _controller.isGrounded)
        {
            speed -= (selectSpeed / smoothDuration) * Time.deltaTime;
        }

        return speed < 0 ? 0 : speed;
    }

    public void Jump()
    {
        if (_controller.isGrounded)
        {
            _verticalVelocity = _jumpForce;
        }
    }

    public void LookForward(bool followCamera)
    {
        if (!PlayerStateMachine.Instance.CanMove())
        {
            return;
        }

        if (followCamera)
        {
            _finalAngle = _mainCamera.transform.eulerAngles.y;
        }

        if (InputManager.Instance.Move)
        {
            if (InputManager.Instance.MoveInput.x > 0)
            {
                _finalAngle += 45;
            }
            else if (InputManager.Instance.MoveInput.x < 0)
            {
                _finalAngle -= 45;
            }
        }

        _model.rotation = Quaternion.Euler(Vector3.up * _finalAngle);
    }

    public void StartLookAt(Transform target)
    {
        if (target == null)
        {
            return;
        }

        UpdateFinalAngleToLookAt(target);

        // Wait for a small delay to lock on to target
        _lookAtTarget = target;
    }

    public void StopLookAt()
    {
        UpdateFinalAngleToLookAt(_lookAtTarget);
        _lookAtTarget = null;
    }

    public void UpdateFinalAngleToLookAt(Transform target)
    {
        if (target == null)
        {
            return;
        }

        // float angle = Mathf.Atan2(target.position.x - transform.position.x, target.position.z - transform.position.z) * Mathf.Rad2Deg;
        // angle = Mathf.Round(angle / 45f);
        // angle *= 45f;
        // _finalAngle = angle;


        // Calculate direction vector in XZ plane (ignoring Y)
        float deltaX = target.position.x - transform.position.x;
        float deltaZ = target.position.z - transform.position.z;

        // For Euler Y rotation, we use Atan2(x, z)
        // This will give you the correct angle to use directly as transform.eulerAngles.y
        float angle = Mathf.Atan2(deltaX, deltaZ) * Mathf.Rad2Deg;

        _finalAngle = angle;
    }

    public bool IsMoving()
    {
        return !VectorUtils.IsVectorZero2D(_moveDirection) && PlayerStateMachine.Instance.CanMove();
    }

    public void StopMoving()
    {
        // ResetDestination(false);
        _moveDirection = new Vector3(0, _moveDirection.y, 0);
    }
    public bool IsJumping()
    {
        return _controller.isGrounded == false;
    }
}
