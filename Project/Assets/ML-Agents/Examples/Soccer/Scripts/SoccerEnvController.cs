using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

public class SoccerEnvController : MonoBehaviour
{
    [System.Serializable]
    public class PlayerInfo
    {
        public AgentSoccer Agent;
        [HideInInspector]
        public Vector3 StartingPos;
        [HideInInspector]
        public Quaternion StartingRot;
        [HideInInspector]
        public Rigidbody Rb;
    }


    /// <summary>
    /// Max Academy steps before this platform resets
    /// </summary>
    [Tooltip("Max Environment Steps")] public int MaxEnvironmentSteps = 25000;
    
    /// <summary>
    /// Enable evaluation logging
    /// </summary>
    [Tooltip("Enable Evaluation Logging")] public bool enableEvaluationLogging = false;
    
    [Tooltip("Evaluation Log File Path")] public string evaluationLogPath = "evaluation_results.txt";
    
    private bool isInferenceMode = false;

    /// <summary>
    /// The area bounds.
    /// </summary>

    /// <summary>
    /// We will be changing the ground material based on success/failue
    /// </summary>

    public GameObject ball;
    [HideInInspector]
    public Rigidbody ballRb;
    Vector3 m_BallStartingPos;

    //List of Agents On Platform
    public List<PlayerInfo> AgentsList = new List<PlayerInfo>();

    private SoccerSettings m_SoccerSettings;


    private SimpleMultiAgentGroup m_BlueAgentGroup;
    private SimpleMultiAgentGroup m_PurpleAgentGroup;

    private int m_ResetTimer;

    void Start()
    {

        m_SoccerSettings = FindFirstObjectByType<SoccerSettings>();
        // Initialize TeamManager
        m_BlueAgentGroup = new SimpleMultiAgentGroup();
        m_PurpleAgentGroup = new SimpleMultiAgentGroup();
        ballRb = ball.GetComponent<Rigidbody>();
        m_BallStartingPos = new Vector3(ball.transform.position.x, ball.transform.position.y, ball.transform.position.z);
        foreach (var item in AgentsList)
        {
            item.StartingPos = item.Agent.transform.position;
            item.StartingRot = item.Agent.transform.rotation;
            item.Rb = item.Agent.GetComponent<Rigidbody>();
            if (item.Agent.team == Team.Blue)
            {
                m_BlueAgentGroup.RegisterAgent(item.Agent);
            }
            else
            {
                m_PurpleAgentGroup.RegisterAgent(item.Agent);
            }
        }
        
        // Check if agents are in inference mode
        CheckInferenceMode();
        
        // Initialize evaluation log file if enabled
        if (enableEvaluationLogging && isInferenceMode)
        {
            InitializeEvaluationLog();
        }
        
        ResetScene();
    }
    
    void CheckInferenceMode()
    {
        // Check if any agent is using an ONNX model (inference mode)
        foreach (var item in AgentsList)
        {
            var behaviorParams = item.Agent.GetComponent<BehaviorParameters>();
            if (behaviorParams != null && behaviorParams.Model != null)
            {
                isInferenceMode = true;
                Debug.Log("Inference mode detected - evaluation logging enabled");
                return;
            }
        }
        isInferenceMode = false;
    }
    
    void InitializeEvaluationLog()
    {
        // Create or clear the evaluation log file
        string fullPath = Path.Combine(Application.dataPath, "..", evaluationLogPath);
        File.WriteAllText(fullPath, "");
        Debug.Log($"Evaluation log initialized at: {fullPath}");
    }
    
    void LogMatchResult(int result)
    {
        if (!enableEvaluationLogging || !isInferenceMode)
            return;
            
        try
        {
            string fullPath = Path.Combine(Application.dataPath, "..", evaluationLogPath);
            File.AppendAllText(fullPath, result.ToString() + "\n");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to log match result: {e.Message}");
        }
    }

    void FixedUpdate()
    {
        m_ResetTimer += 1;
        if (m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            // Log timeout/draw (0)
            LogMatchResult(0);
            
            m_BlueAgentGroup.GroupEpisodeInterrupted();
            m_PurpleAgentGroup.GroupEpisodeInterrupted();
            ResetScene();
        }
    }


    public void ResetBall()
    {
        var randomPosX = Random.Range(-2.5f, 2.5f);
        var randomPosZ = Random.Range(-2.5f, 2.5f);

        ball.transform.position = m_BallStartingPos + new Vector3(randomPosX, 0f, randomPosZ);
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

    }

    public void GoalTouched(Team scoredTeam)
    {
        float winReward = 1 - (float)m_ResetTimer / MaxEnvironmentSteps;
        float loseReward = -1;

        if (scoredTeam == Team.Blue)
        {
            // Log Blue win (1)
            LogMatchResult(1);
            
            m_BlueAgentGroup.AddGroupReward(winReward);
            m_PurpleAgentGroup.AddGroupReward(loseReward);
            
            // Also add individual rewards for PPO compatibility
            foreach (var item in AgentsList)
            {
                if (item.Agent.team == Team.Blue)
                {
                    item.Agent.AddReward(winReward);
                }
                else
                {
                    item.Agent.AddReward(loseReward);
                }
            }
        }
        else
        {
            // Log Purple win (2)
            LogMatchResult(2);
            
            m_PurpleAgentGroup.AddGroupReward(winReward);
            m_BlueAgentGroup.AddGroupReward(loseReward);
            
            // Also add individual rewards for PPO compatibility
            foreach (var item in AgentsList)
            {
                if (item.Agent.team == Team.Purple)
                {
                    item.Agent.AddReward(winReward);
                }
                else
                {
                    item.Agent.AddReward(loseReward);
                }
            }
        }
        
        m_PurpleAgentGroup.EndGroupEpisode();
        m_BlueAgentGroup.EndGroupEpisode();
        ResetScene();

    }


    public void ResetScene()
    {
        m_ResetTimer = 0;

        //Reset Agents
        foreach (var item in AgentsList)
        {
            var randomPosX = Random.Range(-5f, 5f);
            var newStartPos = item.Agent.initialPos + new Vector3(randomPosX, 0f, 0f);
            var rot = item.Agent.rotSign * Random.Range(80.0f, 100.0f);
            var newRot = Quaternion.Euler(0, rot, 0);
            item.Agent.transform.SetPositionAndRotation(newStartPos, newRot);

            item.Rb.linearVelocity = Vector3.zero;
            item.Rb.angularVelocity = Vector3.zero;
        }

        //Reset Ball
        ResetBall();
    }
}
