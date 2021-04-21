using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour {
    public GameObject SwordPrefab;
    public GameObject SwordContainer;
    public GameObject DeadBodyPrefab;
    public GameObject DustPrefab;

    private GameObject PlayerTransform;
    private GameObject Sword;

    public float max_speed;
    public float jump_force;
    public float jump_speed_decrease;
    public float smooth_time;
    public float attack_time;
    public LayerMask ground_layer;

    private Rigidbody2D rb;
    private Animator animator;

    private List<string> animation_keys = new List<string>() {
        "is_running",
        "is_grounded",
        "is_raising",
        "is_falling",
        "is_run_attacking",
        "is_idle_attacking",
        "is_idle_sword_down",
        "is_idle_sword_up",
        "is_die"
    };
    private List<bool> animation_bools = new List<bool>();

    private float input_x = 0f;
	private float input_y = 0f;
    private bool input_jump = false;
    private bool input_attack = false;

    private float velocity_x = 0f;
    public bool face_right = true;

    private float attack_timer = 0f;
    private bool attack_flag = false;

	public bool die_flag = false;

    public Vector3 respawnpoint;
    public LevelManager gameLevelManager;
    public Score ScoreManager;
    private int playerID;
    public int score = 0;

    public bool flip_enable = true;

    void Start() {
        respawnpoint = transform.position;
        gameLevelManager = FindObjectOfType<LevelManager> ();
        ScoreManager = FindObjectOfType<Score> ();
        playerID = gameLevelManager.UpdateID();
        PlayerTransform = GameObject.Find("PlayerTransform");

    }

    void Awake() {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        Transform initial_transform = SwordContainer.transform;
        Sword = Instantiate(SwordPrefab, initial_transform);
        Sword.transform.SetParent(SwordContainer.transform);
    }

    void Update() {
        PlayerTransform.transform.position = rb.transform.position;
        input_x = Input.GetAxisRaw("Horizontal");

		input_y = Input.GetAxisRaw("Vertical");

		if (Input.GetButtonDown("Jump")) {
            input_jump = true;
        }
        if (Input.GetButtonDown("Fire1")) {
            input_attack = true;
        }
    }



    List<bool> get_animation_bools() {
        animation_bools.Clear();
        foreach (string key in animation_keys) {
            animation_bools.Add(animator.GetBool(key));
        }
        animation_bools.Add(face_right);
        return animation_bools;
    }

    void FixedUpdate() {
		if(die_flag){
			input_x = 0f;
			input_y = 0f;
			input_jump = false;
			input_attack = false;
		}
        if ((input_x < 0 && face_right) || (input_x > 0 && !face_right)){
            flip();
        }
        // if(is_walled()) {
        //     Debug.Log("is walled");
        // }

        bool is_running = Mathf.Abs(rb.velocity.x) > 0.1f;
        animator.SetBool("is_running", is_running);
		if(input_y == 0f) {
			animator.SetBool("is_idle_sword_down", false);
			animator.SetBool("is_idle_sword_up", false);
		}
		else if(input_y < 0) {
			animator.SetBool("is_idle_sword_down", true);
		}
		else if(input_y > 0) {
			animator.SetBool("is_idle_sword_up", true);
		}
        if (!is_attacking() && input_attack) {
            if (is_running) {
                animator.SetBool("is_run_attacking", true);
            } else {
                animator.SetBool("is_idle_attacking", true);
            }
            attack_flag = true;
            input_attack = false;
        } else if (!attack_flag) {
            animator.SetBool("is_run_attacking", false);
            animator.SetBool("is_idle_attacking", false);
        }
        float speed;
        if (is_walled()) {
            speed = max_speed;
            animator.SetBool("is_grounded", true);
            animator.SetBool("is_raising", false);
            animator.SetBool("is_falling", false);
            if (input_jump && (input_x != 0)) {
                rb.AddForce(new Vector2(0f, jump_force));
                input_jump = false;
            }
            else if (!is_grounded()){
                input_jump = false;
            }
        }else if (is_grounded()) {
            speed = max_speed;
            animator.SetBool("is_grounded", true);
            animator.SetBool("is_raising", false);
            animator.SetBool("is_falling", false);
            if (input_jump) {
                rb.AddForce(new Vector2(0f, jump_force));
                input_jump = false;
            }
        } else {
            speed = max_speed * jump_speed_decrease;
            animator.SetBool("is_raising", rb.velocity.y > 0f);
            animator.SetBool("is_falling", rb.velocity.y <= 0f);
            animator.SetBool("is_grounded", false);
        }
        float target_position = rb.position.x + input_x * speed * Time.deltaTime;
        Mathf.SmoothDamp(rb.position.x, target_position, ref velocity_x, smooth_time);
        rb.velocity = new Vector2(velocity_x, rb.velocity.y);

        Debug.Log($"my id {Client.instance.id}");
        Vector3 _input = new Vector3(
            GameManager.players[Client.instance.id].transform.position.x,
            GameManager.players[Client.instance.id].transform.position.y,
            0f
        );
        ClientSend.PlayerMovement(_input, get_animation_bools());
        foreach(KeyValuePair<int, PlayerManager> e in GameManager.instance.get_players()) {
            e.Value.idle_timer -= Time.deltaTime;
            Debug.Log($"id:{e.Key} timer:{e.Value.idle_timer}");
            if (e.Key != Client.instance.id && e.Value.idle_timer < 0f) {
                Instantiate(DustPrefab, e.Value.transform.position, Quaternion.identity);
                Destroy(e.Value.me);
                GameManager.instance.remove_player_by_id(e.Key);
            }
        }
        Debug.Log("AAaron" + flip_enable);
    }

    bool is_grounded() {
        float distence = 0.1f;
        Vector2 box_size = new Vector2(0.25f, 0.4f);
        Vector2 Box_position =  new Vector2(rb.position.x, rb.position.y - 0.5f);
        RaycastHit2D hit = Physics2D.BoxCast(Box_position, box_size, 0f, Vector2.down, distence, ground_layer);

        return hit.collider != null;
    }

    bool is_walled() {
        float distence = 0.1f;
        Vector2 box_size = new Vector2(0.15f, 0.05f);
        Vector2 Box_position =  new Vector2(rb.position.x, rb.position.y - 0.4f);
        RaycastHit2D hitleft = Physics2D.BoxCast(Box_position, box_size, 0f, Vector2.left, distence, ground_layer);
        RaycastHit2D hitright = Physics2D.BoxCast(Box_position, box_size, 0f, Vector2.right, distence, ground_layer);

        return ((hitleft.collider != null) || (hitright.collider != null));
    }

    bool is_attacking() {
        if (!attack_flag) {
            return false;
        } else if (attack_timer > attack_time) {
            attack_flag = false;
            attack_timer = 0f;
            return false;
        } else {
            attack_timer += Time.deltaTime;
            return true;
        }
    }

    void flip() {
        if(flip_enable){
            face_right = !face_right;
            transform.Rotate(0f, 180f, 0f);
        }
    }

    public void trigger_dead() {
        // Destroy(Sword, 0f);
        ScoreManager.DeathPunishment(10);
        animator.SetBool("is_die", true);
        die_flag = true;
        StartCoroutine(DelayToInvoke.DelayToInvokeDo(() =>
        {
            // add dead body
            GameObject dead_body = Instantiate(DeadBodyPrefab, rb.position, Quaternion.identity);
            if (Mathf.Abs(rb.transform.rotation.y) > 0.1f) {
                dead_body.transform.Rotate(0f, 180f, 0f);
            }
            Transform initial_transform = SwordContainer.transform;
            // Sword = Instantiate(SwordPrefab, initial_transform);
            // Sword.transform.SetParent(SwordContainer.transform);
            gameLevelManager.Respawn2(gameObject.GetInstanceID());
        }, 2f));
    }

    void OnTriggerEnter2D(Collider2D other){
        if (other.tag == "checkpoint") {
            Vector3 temp = new Vector3(0,2.5f,0);
            respawnpoint = other.transform.position + temp;
        }
        if (other.tag == "sword" && gameObject.tag != "sword") {
            if(other.gameObject.transform.parent.parent.parent.parent.gameObject != gameObject){
                // Animator other_animator = other.gameObject.GetComponent<Animator>();
                // Debug.Log(other_animator.GetBool("is_idle_attacking"));
                //Debug.Log(gameObject.name);
                //Debug.Log(other.gameObject.transform.parent.parent.parent.parent.gameObject.name);
                //trigger_dead();
                //Animator other_animator = other.gameObject.GetComponent<Animator>();
            }
        }
        if (other.tag == "enemydetector") {
            flip_enable = false;
        }
    }

    void OnTriggerExit2D(Collider2D other){
        if (other.tag == "enemydetector") {
            flip_enable = true;
        }
    }

    public int GetPlayerID(){
        return playerID;
    }
}
