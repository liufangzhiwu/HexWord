
namespace Water2D {
	using UnityEngine;
    using UnityEngine.Events;
   	using System.Collections;
	using System.Collections.Generic;
	using UnityEngine.UI;
	//using Apptouch;

#if UNITY_EDITOR
    using UnityEditor;
#endif


    public struct microSpawn{
		public Vector3 pos;
		public int amount;
		public Vector2 initVel;

		public microSpawn(Vector3 pos, int amount, Vector2 initVel)
		{
			this.pos = pos;
			this.amount = amount;
			this.initVel = initVel;
		}
	}

   

    [ExecuteInEditMode]
    [SelectionBase]
    public class Water2D_Spawner : MonoBehaviour
    {

        public enum EmissionType {
            ParticleSystem,
            FillerCollider
        }

        public enum FillerColliderType
        {
            Box,
            Circle,
            Polygon
        }

        public Water2D_Spawner instance;

        public enum EnumTypes
        {
            Regular,
            Refracting,
            Toon
        }

        void Awake()
        {
            if (instance == null)
                instance = this;

            DontDestroyOnLoad(this.gameObject);
            //DontDestroyOnLoad(WaterDropsObjects.);
        }

        // used it to store the unique id between spawners collection.
        public int HashID = -1;

        /// <summary>
        /// The type of Water spawner (Regular/Toon/Refracting)).
        /// </summary>
        public EnumTypes Water2DType = EnumTypes.Regular;

        /// <summary>
        /// The emission type (ParticleSystem/FillerCollider)).
        /// </summary>
        public EmissionType Water2DEmissionType = EmissionType.ParticleSystem;


        /// <summary>
        /// The collider shape type (box, circle, polygon)
        /// </summary>
        public FillerColliderType Water2DFillerType = FillerColliderType.Box;

        /// <summary>
        /// The fill is inverse?
        /// </summary>
        public bool FillerColliderMasked = false;

        /// <summary>
        /// The type of pipeline spawner (Legacy or URP(LWRP)).
        /// </summary>
        public string Water2DRenderType = "";

        /// <summary>
        /// The version of the system.
        /// </summary>
        public string Water2DVersion = "1.2";


        public GameObject DropObject;
        public GameObject[] WaterDropsObjects;

        /// <summary>
        /// The tag of all particles inside this spawner
        /// </summary>
        public string ParticlesTag = "Metaball_liquid";

        
        /// <summary>
        /// This means that the spawner won't be update in every change of state and you can generate the amount of fluid in Editor for use in Play mode later.
        /// When this property is off, the spawner will be refresh at the end of the application runtime.
        /// </summary>
        public bool PersistentFluid = false;

        /// <summary>
        /// The size of each drop.
        /// </summary>
        //[Range (0f,2f)]
        public float size = .45f;


        /// <summary>
        /// If particle can down the scale over lifetime.
        /// </summary>
        public bool ScaleDown = false;


        /// <summary>
        /// The life time of each particle.
        /// </summary>
        //[Range (0f,100f)]
        public float LifeTime = 5f;


        /// <summary>
        /// The delay between particles emission.
        /// </summary>
        //[Range (0f,.3f)]
        public float DelayBetweenParticles = 0.05f;


        // [Header("Trail")]
        /// <summary>
        /// The Trail size on Start.
        /// </summary>
        //[Range(0f, 2f)]
        public float TrailStartSize = .4f;

        /// <summary>
		/// The Trail size on End.
		/// </summary>
        //[Range(0f, 2f)]
        public float TrailEndSize = .4f;

        /// <summary>
		/// The Trail time between Start - End.
		/// </summary>
        //[Range(0f, 2f)]
        public float TrailDelay = .1f;


        /// <summary>
        /// Actual water material.
        /// </summary>
        public Material WaterMaterial;

        /// <summary>
        /// Is shader a Refracting, Toon or Regular style ?
        /// </summary>
        public bool StyleByID;

        /// <summary>
        /// The sorting order ID
        /// </summary>
        public int Sorting;

        /// <summary>
        /// The color category scheme
        /// </summary>
        public int ColorScheme = 1;

        /// <summary>
        /// Fill Color of the particle [Toon/CutoOut shader]
        /// </summary>
        public Color FillColor = new Color(0f, 112 / 255f, 1f);

        /// <summary>
        /// Stroke Color of the particle [Toon/CutoOut shader]
        /// </summary>
        public Color StrokeColor = new Color(4 / 255f, 156 / 255f, 1f);

        public Color _lastStrokeColor = new Color(4 / 255f, 156 / 255f, 1f);

        /// <summary>
        ///Allow blending colors with neighbor particles
        /// </summary>
        public bool Blending = false;

        public bool _lastBlending = false;


        /// <summary>
        /// Threshold alpha value[Toon/CutoOut shader]
        /// </summary>
        public float AlphaCutOff = .2f;

        /// <summary>
        /// Threshold alpha stroke value [Toon/CutoOut shader]
        /// </summary>
        public float AlphaStroke = .2f;

        /// <summary>
        /// Tint Color of the particle [Refracting shader]
        /// </summary>
        public Color TintColor = new Color(0f, 112 / 255f, 1f);

        /// <summary>
        /// Intensity Color of the particle [Refracting shader]
        /// </summary>
        public float Intensity = .5f;

        /// <summary>
        /// Amplify Mag of UV  [Refracting shader]
        /// </summary>
        public float LensMag = 1.2f;

        /// <summary>
        /// Distortion of UV [Refracting shader]
        /// </summary>
        public float Distortion = .5f;

        /// <summary>
        /// Distortion of UV [Refracting shader]
        /// </summary>
        public float DistortionSpeed = .5f;

        /// <summary>
        /// Is glow effect enabled
        /// </summary>
        public bool GlowEffect = false;

        /// <summary>
        /// The color of the glow
        /// </summary>
        public Color GlowColor = new Color(1f,1f,1f,.4f);

        /// <summary>
        /// the spread size of the glow
        /// </summary>
        public float GlowSize = 1.5f;

        /// <summary>
        /// Order in rendering the glow sprite
        /// </summary>
        public int GlowSortingOrder = -1;

        [SerializeField] bool _lastGlowEnabledValue = false;
       

        //[Header("Speed & direction")]
        /// <summary>
        /// The initial speed of particles after spawn.
        /// </summary>
        public Vector2 initSpeed = new Vector2(1f, -1.8f);

        /// <summary>
        /// Amount of speed with which the particle is created
        /// </summary>
        public float Speed = 20f;

        /// <summary>
        /// The physic material
        /// </summary>
        public PhysicsMaterial2D PhysicMat;

        /// <summary>
        /// The size of radius / side of collider circle2D or box2D
        /// </summary>
        public float ColliderSize = 1.5f;

        /// <summary>
        /// The size of radius / side of collider circle2D or box2D
        /// </summary>
        public float LinearDrag = 0f;

        /// <summary>
        /// The size of radius / side of collider circle2D or box2D
        /// </summary>
        public float AngularDrag = 0f;

        /// <summary>
        /// The size of radius / side of collider circle2D or box2D
        /// </summary>
        public float GravityScale = 1f;

        /// <summary>
        /// Constrain rotation on Z axis.
        /// </summary>
        public bool FreezeRotation = false;

        /// <summary>
		/// The X speed limit .
		/// </summary>
		public Vector2 SpeedLimiterX = new Vector2(-300, 300);

        /// <summary>
        /// The Y speed limit .
        /// </summary>
        public Vector2 SpeedLimiterY = new Vector2(-300, 300);


        /// <summary>
        /// The simulation start in awake.
        /// </summary>
        public bool SimulateOnAwake = true;

        /// <summary>
        /// Water system can perform in editor mode.
        /// </summary>
        public bool SimulateInEditor = false;

        /// <summary>
        /// Water system can perform in play mode.
        /// </summary>
        public bool SimulateInPlayMode = false;

        /// <summary>
        /// How many particles in the spawner?
        /// </summary>
        public int DropCount = 100;

        public int _lastDropCount = 100;

        /// <summary>
        /// The responsible to spawn every particle once or repeat the spawn forever
        /// </summary>
        public bool Loop = true;


        /// <summary>
        /// Currently amount of particles useing for the simulation (debug purposes only!)
        /// </summary>
        public int DropsUsed;

        /// <summary>
        /// Apply setup changes over lifetime.
        /// </summary>
        public bool DynamicChanges = true;

        /// <summary>
        /// List of events to call when shape will filled with water particles.
        /// </summary>
        public Water2DEvents OnValidateShapeFill;

        /// <summary>
        /// The collider 2d array to be use to check the fill level.
        /// </summary>
        public Collider2D[] ShapeFillCollider2D;

        /// <summary>
        /// The ShapeFillCollider2D amount.
        /// </summary>
        public int ShapeFillCollider2DCount = 3;

        /// <summary>
        /// The shape fill accuracy 1 = 100%
        /// </summary>
        public float ShapeFillAccuracy = 1f;
        /// <summary>
        /// List of gameobjects that have been collide with water particles .
        /// </summary>
        public Water2DEvents OnCollisionEnterList;

        /// <summary>
        ///  List of gameobjects that will be notified when spawner is about to start .
        /// </summary>
        public Water2DEvents OnSpawnerAboutStart;

        /// <summary>
        ///  List of gameobjects that will be notified when spawner is about to end .
        /// </summary>
        public Water2DEvents OnSpawnerAboutEnd;

        /// <summary>
        ///  List of gameobjects that will be notified when spawner is emitting each particle .
        /// </summary>
        public Water2DEvents OnSpawnerEmitingParticle;

        private void Start()
        {
            StartCoroutine(StartEnumerator());
          
            //Register
            SpawnersManager.ChangeSpawnerValues(instance);

            
        }


        private IEnumerator StartEnumerator()
        {


           yield return new WaitForSeconds(.25f);
            if (Application.isPlaying && SimulateOnAwake && Water2DEmissionType == EmissionType.ParticleSystem)
            {
                Restore();
                yield return new WaitForEndOfFrame();
                Spawn();
            }
            else {
                yield return new WaitForEndOfFrame();
                StartCoroutine(UpdateQuietParticleProperties());
                ColliderFiller cf = GetComponent<ColliderFiller>();
                if(cf != null) cf.Fill ();

            }
           
           

            yield return null;

        }

        void RunSpawner()
		{
            instance.Spawn();

        }
       
        void StopSpawner()
        {
            instance.Restore();

        }

      
       



		public int AllBallsCount{ get; private set;}
		public bool IsSpawning{ get; private set;}

        public bool isRefractingMaterial = false;

        int usableDropsCount;
		int DefaultCount;


		bool _breakLoop = false;

		GameObject _parent;
        string _parentNameID = "Water2DParticlesID_";
       


        public void SetupParticles()
        {
            if (_parent == null && WaterDropsObjects != null)
            {
                if (WaterDropsObjects.Length > 0 && WaterDropsObjects[0] != null)
                    _parent = WaterDropsObjects[0].transform.parent.gameObject;
            }
            if(_parent != null){
               
                DestroyImmediate(_parent);

            }
            _parent = new GameObject(_parentNameID + gameObject.GetInstanceID());
            _parent.transform.hideFlags = HideFlags.HideInHierarchy;

            WaterDropsObjects = new GameObject[DropCount];

            

                
           



            for (int i = 0; i < WaterDropsObjects.Length; i++)
            {
                WaterDropsObjects[i] = Instantiate(DropObject, gameObject.transform.position, new Quaternion(0, 0, 0, 0)) as GameObject;
                WaterDropsObjects[i].GetComponent<MetaballParticleClass>().Active = false;
                WaterDropsObjects[i].GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                WaterDropsObjects[i].transform.SetParent(_parent.transform);
                WaterDropsObjects[i].transform.localScale = new Vector3(size, size, 1f);
                WaterDropsObjects[i].layer = WaterDropsObjects[0].layer;
                WaterDropsObjects[i].tag = ParticlesTag;
                /*
                Transform tt = WaterDropsObjects[i].transform.Find("_glow");
                */

                // Create glow
                if (GlowEffect )
                {
                    _lastGlowEnabledValue = GlowEffect;

                    GameObject o = new GameObject("_glow");
                    o.transform.SetParent(WaterDropsObjects[i].transform);
                    o.transform.localPosition = new Vector3(0, 0, -1f);
                    SpriteRenderer sr = o.AddComponent<SpriteRenderer>();
                    sr.sprite = WaterDropsObjects[i].GetComponent<SpriteRenderer>().sprite;
                    sr.color = GlowColor;
                    sr.sortingOrder = GlowSortingOrder;
                    o.transform.localScale = Vector3.one * GlowSize;

                }
                else {
                    Transform t = WaterDropsObjects[i].transform.Find("_glow");
                    if (t != null)
                        DestroyImmediate(t);
                }
                

                //Set tex color for scheme selection
                Color ColorTex = Color.white;

                if (ColorScheme == 1)
                    ColorTex = new Color(1f,0f,0f);
                if (ColorScheme == 2)
                    ColorTex = new Color(0f, 1f, 0f);
                if (ColorScheme == 3)
                    ColorTex = new Color(0f, 0f, 1f);
                /* if (ColorScheme == 4)
                     ColorTex = new Color(1f, 1f, 0f);
                 if (ColorScheme == 5)
                     ColorTex = new Color(0f, 1f, 1f);
                 if (ColorScheme == 6)
                     ColorTex = new Color(1f, 1f, 1f);


                 if(Water2DType == EnumTypes.Regular || Water2DType == EnumTypes.Refracting) 
                     ColorTex = FillColor;

                 */
                WaterDropsObjects[i].GetComponent<SpriteRenderer>().color = FillColor;
                 WaterDropsObjects[i].GetComponent<TrailRenderer>().startColor = FillColor;
                 WaterDropsObjects[i].GetComponent<TrailRenderer>().endColor = FillColor;
                 
                WaterDropsObjects[i].GetComponent<MetaballParticleClass>().BlendingColor = Blending;

                TrailRenderer tr = WaterDropsObjects[i].GetComponent<TrailRenderer>();
                if (TrailStartSize <= 0f) {
                    tr.enabled = false;
                }
                else {
                    tr.enabled = true;
                    tr.startWidth = TrailStartSize;
                    tr.endWidth = TrailEndSize;
                    tr.time = TrailDelay;
                }

                WaterDropsObjects[i].GetComponent<MetaballParticleClass>().SpawnerParent = this;
            }

           

            AllBallsCount = WaterDropsObjects.Length;

            if(Water2DEmissionType == EmissionType.ParticleSystem)
            {
                DropsUsed *= 0;
                _spawnedDrops *= 0;
            }



            RestoreCheckingFillShape(); // restore events

        }



#if UNITY_EDITOR

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            auxTime = Time.realtimeSinceStartup;
            auxTime += DelayBetweenParticles;
                       
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        float auxTime;
        float NextTick;
        private void OnEditorUpdate()
        {
            if (Application.isPlaying)
                return;


            if (DropObject == null)
                return;

            if (WaterDropsObjects == null || WaterDropsObjects.Length < 1)
            {
                SetupParticles();
            }

            if (WaterDropsObjects[0] == null)
            {
                SetupParticles();
            }

            if (EditorApplication.timeSinceStartup >= NextTick)
            {
               
                //tick  
                NextTick = (float)EditorApplication.timeSinceStartup + DelayBetweenParticles;
                loop_editor(gameObject.transform.position, initSpeed);

                /*
                if (Selection.activeGameObject == gameObject)
                {

                    //print("change color");
                    //CHANGE COLOR
                   
                    //SetRegularWaterparams(FillColor, StrokeColor, AlphaCutOff, AlphaStroke);
                   
                }
                */

            }

            CallShapeFillValidationUpdate();
            

        }
#endif
        private void Update()
        {
            CallShapeFillValidationUpdate();
        }

        void CallShapeFillValidationUpdate()
        { 
        //Check ShapeFill events
            if(OnValidateShapeFill?.GetPersistentEventCount() > 0 && ShapeFillCollider2D != null)
            {
               //StartCheckingFillShape();
            }
        }

        public bool isEnter;
        public void Spawn(bool isenter = false){

			Spawn (DefaultCount);
            isEnter=isenter;
		}

		public void Spawn(int count){
			
            if (DelayBetweenParticles == 0f)
            {
                DropsUsed *= 0;
                SpawnAll();
            }
            else {
                DropsUsed *= 0;
                StartCoroutine(loop(gameObject.transform.position, initSpeed, count));
                StartCheckingFillShape();
            }
			
		}

        public void SpawnAll() {
            SpawnAllParticles(gameObject.transform.position, initSpeed, DefaultCount);
        }

		public void Spawn(int count, Vector3 pos){
		
			StartCoroutine (loop(pos, initSpeed, count));
		}

		public void Spawn(int count, Vector3 pos, Vector2 InitVelocity, float delay = 0f){
			
			StartCoroutine (loop(pos, InitVelocity, count, delay));
		}

		
        public void StopSpawning()
        {
            _breakLoop = true;
            IsSpawning = false;
        }

		public void Restore()
		{

			IsSpawning = false;
			_breakLoop = true;
            DropsUsed *= 0;

			


			for (int i = 0; i < WaterDropsObjects.Length; i++) {

                if (WaterDropsObjects[i] != null) {
                    if (WaterDropsObjects[i].GetComponent<MetaballParticleClass>().Active == true)
                    {
                        WaterDropsObjects[i].GetComponent<MetaballParticleClass>().Active = false;
                    }
                    WaterDropsObjects[i].GetComponent<MetaballParticleClass>().witinTarget = false;
                }

                		
			}




			//gameObject.transform.localEulerAngles = Vector3.zero;
			//initSpeed = new Vector2 (0, -2f);

			DefaultCount = AllBallsCount;
			usableDropsCount = DefaultCount;
			//Dynamic = false;
		}

        int _spawnedDrops = 0;
        Color _lastFillColor;
        void loop_editor(Vector3 _pos, Vector2 _initSpeed, int count = -1, float delay = 0f, bool waitBetweenDropSpawn = true)
        {
            if (Application.isPlaying)
                return;

            if (Water2DEmissionType == EmissionType.FillerCollider)
            {
                //Debug.LogError("You're trying spawn particles in a Filler type. You should create a water spawner instead");
                return;
            }

            if (!SimulateInEditor)
                return;

            if (WaterDropsObjects == null || WaterDropsObjects.Length < 1) {
                SetupParticles();
                return;
            }


            for (int i = 0; i < WaterDropsObjects.Length; i++)
            {
                if (WaterDropsObjects[i] == null)
                    return;

                MetaballParticleClass MetaBall = WaterDropsObjects[i].GetComponent<MetaballParticleClass>();

                //CHANGE COLOR
               
                    if (MetaBall.Active == false)
                        MetaBall.gameObject.GetComponent<SpriteRenderer>().color = FillColor;

                SetRegularWaterparams(FillColor, StrokeColor, AlphaCutOff, AlphaStroke);
               

                if (MetaBall.Active == true)
                    continue;

                _canInvokeAttheEnd = true;

                if (LifeTime <= 0) {
                    MetaBall.LifeTime = -1f;
                }
                else {
                    MetaBall.LifeTime = LifeTime;
                }
               
                WaterDropsObjects[i].transform.position = transform.position;
                MetaBall.Active = true;
                MetaBall.witinTarget = false;

                if (_initSpeed == Vector2.zero)
                    _initSpeed = initSpeed;

                if (true)
                {
                    _initSpeed = initSpeed;
                    MetaBall.transform.localScale = new Vector3(size, size, 1f);

                   
                    

                    TrailRenderer tr = WaterDropsObjects[i].GetComponent<TrailRenderer>();
                    if (TrailStartSize <= 0f)
                    {
                        tr.enabled = false;
                    }
                    else
                    {
                        tr.enabled = true;
                        tr.startWidth = TrailStartSize;
                        tr.endWidth = TrailEndSize;
                        tr.time = TrailDelay;
                    }

                    MetaBall.Velocity_Limiter_X = SpeedLimiterX;
                    MetaBall.Velocity_Limiter_Y = SpeedLimiterY;

                    Rigidbody2D rb = MetaBall.GetComponent<Rigidbody2D>();
                    rb.sharedMaterial = PhysicMat;
                    rb.drag = LinearDrag;
                    rb.drag = AngularDrag;
                    rb.gravityScale = GravityScale;

                    if (FreezeRotation)
                        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

                    MetaBall.GetComponent<CircleCollider2D>().sharedMaterial = PhysicMat;
                    MetaBall.GetComponent<CircleCollider2D>().radius = ColliderSize;

                    MetaBall.ScaleDown = ScaleDown;

                    //CHANGE COLOR IN REALTIME
              
                    if (MetaBall.Active == false)
                       MetaBall.gameObject.GetComponent<SpriteRenderer>().color = FillColor;

                    SetRegularWaterparams(FillColor, StrokeColor, AlphaCutOff, AlphaStroke);
                   

                    // CHANGE GLOW IN REALTIME
                    if (GlowEffect && MetaBall.glowSP != null)
                    {
                        if (MetaBall.glowSP.color != GlowColor)
                            MetaBall.glowSP.color = GlowColor;

                        if (MetaBall.glowSP.sortingOrder != GlowSortingOrder)
                            MetaBall.glowSP.sortingOrder = GlowSortingOrder;

                        if (MetaBall.glowSP.transform.localScale.x != GlowSize)
                            MetaBall.glowSP.transform.localScale = Vector3.one * GlowSize;
                    }
                }

                Vector2 dir = transform.up;
                MetaBall.GetComponent<Rigidbody2D>().velocity = -dir * Speed;


                DropsUsed++;
                _spawnedDrops++;

                //Invoke event
                InvokeOnSpawnerEmittinEachParticle(gameObject, MetaBall.gameObject);

                if (_spawnedDrops >= DropCount)
                {

                    if(!Loop)
                        SimulateInEditor = false;

                    //Invoke event End
                    if (_canInvokeAttheEnd)
                    {
                        InvokeOnSpawnerEnd(gameObject);
                        _canInvokeAttheEnd = false;
                    }


                    _spawnedDrops *= 0;
                }


                return;
            }

           

           
        }

        bool _canInvokeAttheEnd = true;
        IEnumerator loop(Vector3 _pos, Vector2 _initSpeed, int count = -1, float delay = 0f, bool waitBetweenDropSpawn = true){

            if (IsSpawning)
                yield break;

            if (Water2DEmissionType == EmissionType.FillerCollider)
            { Debug.LogError("You're trying spawn particles in a Filler type. You should create a water spawner instead"); yield break; }

            //Physics2D.autoSimulation = true;

            IsSpawning = true;

            yield return new WaitForSeconds (delay);

			_breakLoop = false;

			
			//int auxCount = 0;

            //Invoke event Start
            InvokeOnSpawnerStart(gameObject);

            while (true) {
				for (int i = 0; i < WaterDropsObjects.Length; i++) {

					if (_breakLoop)
						yield break;

					MetaballParticleClass MetaBall = WaterDropsObjects [i].GetComponent<MetaballParticleClass> ();

					if (MetaBall.Active == true)
						continue;

                    _canInvokeAttheEnd = true; 

                    if (LifeTime <= 0)
                    {
                        MetaBall.LifeTime = -1f;
                    }
                    else
                    {
                        MetaBall.LifeTime = LifeTime;
                    }

                    WaterDropsObjects [i].transform.position = transform.position;
					

					if (_initSpeed == Vector2.zero)
						_initSpeed = initSpeed;

					if (DynamicChanges) {
						_initSpeed = initSpeed;
						MetaBall.transform.localScale = new Vector3 (size, size, 1f);

                        //CHANGE COLOR
                            
                         _lastFillColor = FillColor;
                         if (MetaBall.Active == false)
                            MetaBall.gameObject.GetComponent<SpriteRenderer>().color = FillColor;

                        SetRegularWaterparams(FillColor, StrokeColor, AlphaCutOff, AlphaStroke);
                       

                        TrailRenderer tr = WaterDropsObjects[i].GetComponent<TrailRenderer>();
                        if (TrailStartSize <= 0f)
                        {
                            tr.enabled = false;
                        }
                        else
                        {
                            tr.enabled = true;
                            tr.startWidth = TrailStartSize;
                            tr.endWidth = TrailEndSize;
                            tr.time = TrailDelay;
                        }


                        MetaBall.Velocity_Limiter_X = SpeedLimiterX;
                        MetaBall.Velocity_Limiter_Y = SpeedLimiterY;

                        Rigidbody2D rb = MetaBall.GetComponent<Rigidbody2D>();
                        rb.sharedMaterial = PhysicMat;
                        rb.drag = LinearDrag;
                        rb.angularDrag = AngularDrag;
                        rb.gravityScale = GravityScale;

                        if (FreezeRotation)
                            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

                        MetaBall.GetComponent<CircleCollider2D>().sharedMaterial = PhysicMat;
                        MetaBall.GetComponent<CircleCollider2D>().radius = ColliderSize;
                        MetaBall.ScaleDown = ScaleDown;


                    }

                    MetaBall.Active = true;
                    MetaBall.witinTarget = false; 


                    //WaterDropsObjects [i].GetComponent<Rigidbody2D> ().velocity = _initSpeed;
                    Vector2 dir = transform.up;
                    MetaBall.GetComponent<Rigidbody2D>().velocity = -dir * Speed;

                    DropsUsed++;
                    _spawnedDrops++;




                    //print(Time.fixedDeltaTime / DelayBetweenParticles);
                    if (waitBetweenDropSpawn) {
                        if (DelayBetweenParticles < 0.001f)
                            yield return null;
                        else
                            yield return new WaitForSeconds(DelayBetweenParticles);

                    }
						

                    //Invoke event
                    InvokeOnSpawnerEmittinEachParticle(gameObject, MetaBall.gameObject);

                }
				yield return new WaitForEndOfFrame ();

                if (_spawnedDrops >= DropCount)
                {
                    //Invoke event End
                    if (_canInvokeAttheEnd)
                    {
                        InvokeOnSpawnerEnd(gameObject);
                        _canInvokeAttheEnd = false;
                    }

                    _spawnedDrops *= 0;

                    if (!Loop)
                        yield break;
                }


			}
		}

        /// <summary>
        /// Spawn all particles together at the same time
        /// </summary>
        /// <param name="_pos"></param>
        /// <param name="_initSpeed"></param>
        /// <param name="count"></param>
        /// <param name="delay"></param>
        void SpawnAllParticles(Vector3 _pos, Vector2 _initSpeed, int count = -1, float delay = 0f)
        {
           

            IsSpawning = true;

            int auxCount = 0;
           // while (true)
            //{
                for (int i = 0; i < WaterDropsObjects.Length; i++)
                {


                    MetaballParticleClass MetaBall = WaterDropsObjects[i].GetComponent<MetaballParticleClass>();

                    if (MetaBall.Active == true)
                        continue;

                    MetaBall.LifeTime = LifeTime;
                    WaterDropsObjects[i].transform.position = transform.position;
                    MetaBall.Active = true;
                    MetaBall.witinTarget = false;

                    if (_initSpeed == Vector2.zero)
                        _initSpeed = initSpeed;

                    if (DynamicChanges)
                    {
                        _initSpeed = initSpeed;
                        MetaBall.transform.localScale = new Vector3(size, size, 1f);
                    //CHANGE COLOR
                   
                    //_lastFillColor = FillColor;
                    if (MetaBall.Active == false)
                       MetaBall.gameObject.GetComponent<SpriteRenderer>().color = FillColor;

                        SetRegularWaterparams(FillColor, StrokeColor, AlphaCutOff, AlphaStroke);
                   

                }

                WaterDropsObjects[i].GetComponent<Rigidbody2D>().velocity = _initSpeed;


                    // Count limiter
                    if (count > -1)
                    {
                        auxCount++;
                        if (auxCount >= count && !Loop)
                        {
                            break;
                        }
                    }

                    

                }
               
                //alreadySpawned = true;
           // }
        }

        public void InvokeOnShapeFill(GameObject obj, GameObject results)
        {
            OnValidateShapeFill?.Invoke(obj, results);
        }

        public void InvokeOnCollisionEnter2D(GameObject obj, GameObject other)
        {
            OnCollisionEnterList.Invoke(obj, other);

        }

        public void InvokeOnSpawnerStart(GameObject obj)
        {
            if(OnSpawnerAboutStart != null)
                OnSpawnerAboutStart.Invoke(obj, null);

        }

        public void InvokeOnSpawnerEnd(GameObject obj)
        {
            OnSpawnerAboutEnd.Invoke(obj, null);

        }

        public void InvokeOnSpawnerEmittinEachParticle(GameObject obj, GameObject obj2)
        {
            OnSpawnerEmitingParticle?.Invoke(obj, obj2);

        }



        Camera fresnelCamera;
        int _lastSorting;
        public void SetRegularWaterparams(Color fill, Color fresnel, float alphaCutoff, float multiplier)
        {


            /*
            WaterMaterial.SetFloat("_constant", multiplier);
            WaterMaterial.SetFloat("_botmcut", alphaCutoff);
           
            */

            SpawnersManager.ChangeSpawnerValues(instance);

            // I do use Stroke as Fresnel
            // Fresnel is a shared property since I've use camera to reach that effect
            if(_lastStrokeColor != StrokeColor) {
                _lastStrokeColor = StrokeColor;
              // SpawnersManager.SetFresnelColor(StrokeColor);
            }

            if (_lastSorting != Sorting)
            {
                _lastSorting = Sorting;
                SpawnersManager.SetSorting(Sorting);
            }
        }
        /*
        public void SetToonWaterparams(Color fill, Color stroke, float alphaCutoff, float alphaStroke)
		{
            
            WaterMaterial.SetColor ("_Color", fill);
            WaterMaterial.SetColor ("_StrokeColor", stroke);
            WaterMaterial.SetFloat("_Cutoff", alphaCutoff);
            WaterMaterial.SetFloat("__Cutoff", alphaCutoff);
            WaterMaterial.SetFloat("_Stroke", alphaStroke);
            


           // SpawnersManager.SetColorScheme(ColorScheme, WaterMaterial, fill,stroke, alphaCutoff, alphaStroke);

            if (_lastSorting != Sorting)
            {
                _lastSorting = Sorting;
                SpawnersManager.SetSorting(Sorting);
            }
           

            SpawnersManager.ChangeSpawnerValues(instance);

            // I do use Stroke as Fresnel
            // Fresnel is a shared property since I've use camera to reach that effect
            if (_lastStrokeColor != StrokeColor)
            {
                _lastStrokeColor = StrokeColor;
                // SpawnersManager.SetFresnelColor(StrokeColor);
            }

            if (_lastSorting != Sorting)
            {
                _lastSorting = Sorting;
                SpawnersManager.SetSorting(Sorting);
            }

        }

        public void SetRefractingWaterparams(float intensity, float mag, float speed)
        {
            
            //WaterMaterial.SetColor("_TintColor", tint);
            WaterMaterial.SetFloat("_AmountOfTintColor", intensity);
            WaterMaterial.SetFloat("_Mag", mag*10f);
            WaterMaterial.SetFloat("_Speed", speed*10f);

            // I do use Stroke as Fresnel
            // Fresnel is a shared property since I've use camera to reach that effect
            if (_lastStrokeColor != StrokeColor)
            {
                _lastStrokeColor = StrokeColor;
               // SpawnersManager.SetFresnelColor(StrokeColor);
            }

            if (_lastSorting != Sorting)
            {
                _lastSorting = Sorting;
                SpawnersManager.SetSorting(Sorting);
            }

        }
        */
        IEnumerator UpdateQuietParticleProperties()
        {
            while (true)
            {
                for (int i = 0; i < WaterDropsObjects.Length; i++)
                {
                    MetaballParticleClass MetaBall = WaterDropsObjects[i].GetComponent<MetaballParticleClass>();
                    _lastFillColor = FillColor;
                    if (MetaBall.Active == false)
                        MetaBall.gameObject.GetComponent<SpriteRenderer>().color = FillColor;

                    SetRegularWaterparams(FillColor, StrokeColor, AlphaCutOff, AlphaStroke);

                    MetaBall.Velocity_Limiter_X = SpeedLimiterX;
                    MetaBall.Velocity_Limiter_Y = SpeedLimiterY;

                   

                    Rigidbody2D rb = MetaBall.GetComponent<Rigidbody2D>();
                    rb.sharedMaterial = PhysicMat;
                    rb.drag = LinearDrag;
                    rb.angularDrag = AngularDrag;
                    rb.gravityScale = GravityScale;

                    if (FreezeRotation)
                        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

                    MetaBall.GetComponent<CircleCollider2D>().sharedMaterial = PhysicMat;
                    MetaBall.GetComponent<CircleCollider2D>().radius = ColliderSize;



                }
                yield return null;
            }
        }


        public Material GetCurrentMaterial()
        {
            return WaterMaterial;
        }

        void StartCheckingFillShape()
        { 
            if(!_checkOnFillRunning && !_checkOnFillComplete)
            {
                print("StartCheckingFillShape");
                StartCoroutine(CheckOnFill(ShapeFillCollider2D, ShapeFillAccuracy));
            }
        }

        public void RestoreCheckingFillShape()
        {
            StartCoroutine(_restoreCheckingFillShapeEnum());
        }
        
        IEnumerator _restoreCheckingFillShapeEnum()
        {
            yield return new WaitForSeconds(.2f);
            _breakCheckOnFill = true;
            _checkOnFillComplete = false;

            if(ShapeFillCollider2DCount != ShapeFillCollider2D.Length)
            ShapeFillCollider2D = new Collider2D[ShapeFillCollider2DCount];
        }

        // 表示填充检查是否正在运行
        bool _checkOnFillRunning = false;
        // 表示是否需要中断检查
        bool _breakCheckOnFill = false;
        // 表示填充检查是否完成
        bool _checkOnFillComplete = false;

        // 协程：检查填充状态
        IEnumerator CheckOnFill(Collider2D []shapeCollider, float accuracy = .8f)
        {
            // 标记填充检查正在运行
            _checkOnFillRunning = true;

            // 创建一个接触过滤器，用于碰撞检测
            ContactFilter2D cf = new ContactFilter2D();
            cf.useTriggers = true; // 使用触发器
            // 设置碰撞层为 DropObject 的层
            cf.SetLayerMask(Physics2D.GetLayerCollisionMask(DropObject.layer));
            cf.useLayerMask = true; // 使用层过滤

            // 存储所有重叠的碰撞体
            Collider2D[] allOverlappingColliders = new Collider2D[DropCount];

            int result = 0; // 存储重叠碰撞体的数量

            // 无限循环，直到手动中断
            while (true)
            {
                // 检查是否需要中断
                if (_breakCheckOnFill)
                {
                    _checkOnFillComplete = false;
                    _checkOnFillRunning = false; // 标记检查不再运行
                    _breakCheckOnFill = false; // 重置中断标志
                    yield break; // 结束协程
                }

                yield return new WaitForFixedUpdate(); // 等待固定更新（物理帧）

                // 遍历所有形状碰撞体
                //for (int i = 0; i < shapeCollider.Length; i++)
                //{
                //    //Debug.Log(shapeCollider[i]); // 打印当前碰撞体的信息
//
                //    if (shapeCollider[i] == null) continue; // 如果碰撞体为空，则跳过
//
                //    // 检测当前碰撞体重叠的其他碰撞体数量
                //    result = shapeCollider[i].Overlap(cf, allOverlappingColliders);
                //    bool _trigged = false; // 标记是否触发事件
//
                //    // 根据不同的发射类型判断填充是否触发
                //    //if (Water2DEmissionType == EmissionType.FillerCollider)
                //    //{
                //    //    // 如果是填充碰撞体，检查重叠数量是否达到预期
                //    //    _trigged = (result >= DropsUsed * accuracy);
                //    //}
                //    //else
                //    //{
                //    //    // 否则，检查重叠数量是否达到目标数量
                //    //    _trigged = (result >= DropCount * accuracy);
                //    //}
//
                //    // 如果条件满足，调用填充事件
                //    //if (_trigged)
                //    //{
                //    //    InvokeOnShapeFill(instance.gameObject, shapeCollider[i].gameObject); // 触发填充事件
                //    //    Debug.Log("Fill Event successful Complete! : droplives:" + DropsUsed + "   // " + ((int)(DropsUsed * accuracy)) + " within the target");
                //    //    _checkOnFillComplete = true; // 标记检查完成
                //    //    _breakCheckOnFill = true; // 中断检查
                //    //}
                //}
                //int count = DropsUsed + WaterManager.instance.waterParCount;
                //Debug.Log("Fill" + count + "// " + SaveSystem.Instance.UserData.signid);
                
                // 如果条件满足，调用填充事件
                if (isEnter)
                {
                    if (DropsUsed==GameDataManager.Instance.UserData.signid*60)
                    {
                        WaterManager.instance.TriggerWaterline(DropsUsed);
                        _checkOnFillComplete = true; // 标记检查完成
                        _breakCheckOnFill = true; // 中断检查
                        isEnter=false;
                    }
                   
                }else if (DropsUsed+WaterManager.instance.waterParCount==(GameDataManager.Instance.UserData.signid)*60)
                {
                    WaterManager.instance.TriggerWaterline(DropsUsed);
                    //InvokeOnShapeFill(instance.gameObject, shapeCollider[0].gameObject); // 触发填充事件
                    //Debug.Log("Fill Event successful Complete! : droplives:" + DropsUsed + "   // " + ((int)(DropsUsed * accuracy)) + " within the target");
                    _checkOnFillComplete = true; // 标记检查完成
                    _breakCheckOnFill = true; // 中断检查
                }
            }
        }


        private void OnDestroy()
        {
            SpawnersManager.DeleteSpawnerValues(instance);
            DestroyImmediate(instance._parent);
            //StartCoroutine(_destroyItself());
        }
        
    }

}