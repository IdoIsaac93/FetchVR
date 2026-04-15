using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FetchEvaluationRunner : MonoBehaviour
{
    [Serializable]
    public class EvaluationCase
    {
        public string name = "case";
        public int goalSpawnAreaIndex;
        public int ballSpawnAreaIndex;
        public int agentSpawnAreaIndex;
        public float agentYaw;
    }

    [Header("References")]
    [SerializeField] private FetchAgent fetchAgent;
    [SerializeField] private FetchArea fetchArea;

    [Header("Run Settings")]
    [SerializeField] private bool runOnStart;
    [SerializeField] private float maxEpisodeSeconds = 20f;
    [SerializeField] private float delayBetweenCases = 0.25f;
    [SerializeField] private List<EvaluationCase> cases = new List<EvaluationCase>();

    [Header("Debug")]
    [SerializeField] private int currentCaseIndex = -1;
    [SerializeField] private bool isRunning;
    [SerializeField] private int successCount;
    [SerializeField] private int failureCount;

    private Coroutine m_RunCoroutine;
    private bool m_CaseFinished;
    private bool m_CaseSucceeded;

    [ContextMenu("Populate Default 30 Cases")]
    private void PopulateDefault30Cases()
    {
        cases = new List<EvaluationCase>
        {
            new EvaluationCase { name = "easy_01", goalSpawnAreaIndex = 0, ballSpawnAreaIndex = 1, agentSpawnAreaIndex = 0, agentYaw = 0f },
            new EvaluationCase { name = "easy_02", goalSpawnAreaIndex = 1, ballSpawnAreaIndex = 0, agentSpawnAreaIndex = 1, agentYaw = 180f },
            new EvaluationCase { name = "easy_03", goalSpawnAreaIndex = 2, ballSpawnAreaIndex = 3, agentSpawnAreaIndex = 2, agentYaw = 90f },
            new EvaluationCase { name = "easy_04", goalSpawnAreaIndex = 3, ballSpawnAreaIndex = 2, agentSpawnAreaIndex = 3, agentYaw = 270f },
            new EvaluationCase { name = "easy_05", goalSpawnAreaIndex = 0, ballSpawnAreaIndex = 0, agentSpawnAreaIndex = 1, agentYaw = 45f },
            new EvaluationCase { name = "easy_06", goalSpawnAreaIndex = 1, ballSpawnAreaIndex = 1, agentSpawnAreaIndex = 2, agentYaw = 225f },
            new EvaluationCase { name = "easy_07", goalSpawnAreaIndex = 2, ballSpawnAreaIndex = 2, agentSpawnAreaIndex = 1, agentYaw = 135f },
            new EvaluationCase { name = "easy_08", goalSpawnAreaIndex = 3, ballSpawnAreaIndex = 3, agentSpawnAreaIndex = 0, agentYaw = 315f },
            new EvaluationCase { name = "easy_09", goalSpawnAreaIndex = 0, ballSpawnAreaIndex = 1, agentSpawnAreaIndex = 3, agentYaw = 90f },
            new EvaluationCase { name = "easy_10", goalSpawnAreaIndex = 2, ballSpawnAreaIndex = 3, agentSpawnAreaIndex = 0, agentYaw = 180f },

            new EvaluationCase { name = "mid_01", goalSpawnAreaIndex = 0, ballSpawnAreaIndex = 4, agentSpawnAreaIndex = 1, agentYaw = 0f },
            new EvaluationCase { name = "mid_02", goalSpawnAreaIndex = 4, ballSpawnAreaIndex = 1, agentSpawnAreaIndex = 5, agentYaw = 180f },
            new EvaluationCase { name = "mid_03", goalSpawnAreaIndex = 2, ballSpawnAreaIndex = 5, agentSpawnAreaIndex = 3, agentYaw = 90f },
            new EvaluationCase { name = "mid_04", goalSpawnAreaIndex = 5, ballSpawnAreaIndex = 2, agentSpawnAreaIndex = 4, agentYaw = 270f },
            new EvaluationCase { name = "mid_05", goalSpawnAreaIndex = 1, ballSpawnAreaIndex = 4, agentSpawnAreaIndex = 0, agentYaw = 45f },
            new EvaluationCase { name = "mid_06", goalSpawnAreaIndex = 4, ballSpawnAreaIndex = 3, agentSpawnAreaIndex = 2, agentYaw = 225f },
            new EvaluationCase { name = "mid_07", goalSpawnAreaIndex = 3, ballSpawnAreaIndex = 5, agentSpawnAreaIndex = 1, agentYaw = 135f },
            new EvaluationCase { name = "mid_08", goalSpawnAreaIndex = 5, ballSpawnAreaIndex = 0, agentSpawnAreaIndex = 2, agentYaw = 315f },
            new EvaluationCase { name = "mid_09", goalSpawnAreaIndex = 2, ballSpawnAreaIndex = 4, agentSpawnAreaIndex = 5, agentYaw = 180f },
            new EvaluationCase { name = "mid_10", goalSpawnAreaIndex = 1, ballSpawnAreaIndex = 5, agentSpawnAreaIndex = 4, agentYaw = 0f },

            new EvaluationCase { name = "hard_01", goalSpawnAreaIndex = 0, ballSpawnAreaIndex = 7, agentSpawnAreaIndex = 1, agentYaw = 0f },
            new EvaluationCase { name = "hard_02", goalSpawnAreaIndex = 7, ballSpawnAreaIndex = 0, agentSpawnAreaIndex = 6, agentYaw = 180f },
            new EvaluationCase { name = "hard_03", goalSpawnAreaIndex = 1, ballSpawnAreaIndex = 6, agentSpawnAreaIndex = 0, agentYaw = 90f },
            new EvaluationCase { name = "hard_04", goalSpawnAreaIndex = 6, ballSpawnAreaIndex = 1, agentSpawnAreaIndex = 7, agentYaw = 270f },
            new EvaluationCase { name = "hard_05", goalSpawnAreaIndex = 2, ballSpawnAreaIndex = 7, agentSpawnAreaIndex = 3, agentYaw = 45f },
            new EvaluationCase { name = "hard_06", goalSpawnAreaIndex = 7, ballSpawnAreaIndex = 2, agentSpawnAreaIndex = 4, agentYaw = 225f },
            new EvaluationCase { name = "hard_07", goalSpawnAreaIndex = 3, ballSpawnAreaIndex = 6, agentSpawnAreaIndex = 2, agentYaw = 135f },
            new EvaluationCase { name = "hard_08", goalSpawnAreaIndex = 6, ballSpawnAreaIndex = 3, agentSpawnAreaIndex = 5, agentYaw = 315f },
            new EvaluationCase { name = "hard_09", goalSpawnAreaIndex = 0, ballSpawnAreaIndex = 6, agentSpawnAreaIndex = 7, agentYaw = 180f },
            new EvaluationCase { name = "hard_10", goalSpawnAreaIndex = 1, ballSpawnAreaIndex = 7, agentSpawnAreaIndex = 6, agentYaw = 0f }
        };
    }

    private void OnEnable()
    {
        if (fetchAgent != null)
        {
            fetchAgent.OnTrainingEpisodeFinished += HandleTrainingEpisodeFinished;
        }
    }

    private void OnDisable()
    {
        if (fetchAgent != null)
        {
            fetchAgent.OnTrainingEpisodeFinished -= HandleTrainingEpisodeFinished;
        }
    }

    private void Start()
    {
        if (runOnStart)
        {
            RunEvaluation();
        }
    }

    [ContextMenu("Run Evaluation")]
    public void RunEvaluation()
    {
        if (fetchAgent == null || fetchArea == null)
        {
            Debug.LogError("[FetchEval] Assign FetchAgent and FetchArea before running.");
            return;
        }

        if (cases.Count == 0)
        {
            Debug.LogWarning("[FetchEval] No evaluation cases configured.");
            return;
        }

        if (m_RunCoroutine != null)
        {
            StopCoroutine(m_RunCoroutine);
        }

        m_RunCoroutine = StartCoroutine(RunEvaluationRoutine());
    }

    [ContextMenu("Stop Evaluation")]
    public void StopEvaluation()
    {
        if (m_RunCoroutine != null)
        {
            StopCoroutine(m_RunCoroutine);
            m_RunCoroutine = null;
        }

        isRunning = false;
    }

    private IEnumerator RunEvaluationRoutine()
    {
        isRunning = true;
        successCount = 0;
        failureCount = 0;

        Debug.Log($"[FetchEval] Starting evaluation with {cases.Count} cases.");

        for (int i = 0; i < cases.Count; i++)
        {
            currentCaseIndex = i;
            EvaluationCase evaluationCase = cases[i];

            m_CaseFinished = false;
            m_CaseSucceeded = false;

            fetchAgent.ResetForEvaluation(
                evaluationCase.goalSpawnAreaIndex,
                evaluationCase.ballSpawnAreaIndex,
                evaluationCase.agentSpawnAreaIndex,
                evaluationCase.agentYaw);

            float startTime = Time.realtimeSinceStartup;
            while (!m_CaseFinished && Time.realtimeSinceStartup - startTime < maxEpisodeSeconds)
            {
                yield return null;
            }

            float elapsed = Time.realtimeSinceStartup - startTime;
            if (!m_CaseFinished)
            {
                failureCount++;
                Debug.LogWarning($"[FetchEval] {evaluationCase.name} timed out after {elapsed:F2}s.");
            }
            else if (m_CaseSucceeded)
            {
                successCount++;
                Debug.Log($"[FetchEval] {evaluationCase.name} succeeded in {elapsed:F2}s.");
            }
            else
            {
                failureCount++;
                Debug.Log($"[FetchEval] {evaluationCase.name} failed in {elapsed:F2}s.");
            }

            if (delayBetweenCases > 0f)
            {
                yield return new WaitForSeconds(delayBetweenCases);
            }
        }

        isRunning = false;
        m_RunCoroutine = null;

        float successRate = cases.Count > 0 ? (float)successCount / cases.Count : 0f;
        Debug.Log($"[FetchEval] Complete. Success: {successCount}/{cases.Count} ({successRate:P1}), Failure: {failureCount}.");
    }

    private void HandleTrainingEpisodeFinished(bool succeeded)
    {
        if (!isRunning)
        {
            return;
        }

        m_CaseSucceeded = succeeded;
        m_CaseFinished = true;
    }
}
