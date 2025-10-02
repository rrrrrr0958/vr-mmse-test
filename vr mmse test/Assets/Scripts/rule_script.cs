using UnityEngine;
using System.Collections;
using TMPro;
using Unity.XR.CoreUtils; // �����ޤJ���R�W�Ŷ��~��ϥ� XR Origin ����

public class Rule_script : MonoBehaviour
{
    // =================================================================
    // VR �y�{ & XR �t�γ]�w (�ݭn�b Inspector ���즲�]�w)
    // =================================================================

    [Header("VR ��v���P XR Origin")]
    [Tooltip("�������� XR Origin �ڪ���")]
    public XROrigin xrOrigin;

    [Tooltip("VR Camera/Head �� Transform ���� (�Ω������a��e��V)")]
    public Transform vrCameraTransform;

    // =================================================================
    // UI & �y���]�w
    // =================================================================

    [Header("UI & 3D ����")]
    [Tooltip("�Ω���ܳW�h��r�� TextMeshPro ���� (3D �� World Space Canvas)")]
    public TextMeshPro RuleText_rule;

    [Tooltip("�b�W�h�������q�n��ܪ��I��/���ܪ���")]
    public GameObject treasurebg_rule;

    [Header("�y���]�w")]
    [Tooltip("����y���Ϊ� AudioSource ����")]
    public AudioSource voiceAudioSource;

    [Tooltip("�C�q�y���ɪ� Clip (�зӶ��ǩ즲)")]
    public AudioClip[] ruleClips;

    // =================================================================
    // �ɶ��]�w
    // =================================================================

    [Header("�ɶ��]�w")]
    [Tooltip("�C���}�l�ɪ���l������")]
    public float initialDelaySeconds = 3f;

    [Tooltip("�b 'treasurebg_rule' ��ܫᵥ�ݪ����")]
    public float treasureDisplaySeconds = 3f;

    // =================================================================
    // �������
    // =================================================================

    private string[] ruleTexts = new string[]
    {
        "�w��Ө�VR�ֶ�",
        "�ڭ̷ǳƤF�@�t�C���D�ԥ���",
        "���Ȧ��\��i�H��o�_�c���_��",
        "�{�b���Ӫ��D�D�Ԫ��W�h"
    };

    // =================================================================
    // Unity �ͩR�g����k
    // =================================================================

    void Start()
    {
        // ��l�ơG�T�O UI �����M�I���O���ê�
        if (RuleText_rule != null) RuleText_rule.gameObject.SetActive(false);
        if (treasurebg_rule != null) treasurebg_rule.SetActive(false);

        // �ˬd����]�w
        if (voiceAudioSource == null || RuleText_rule == null || xrOrigin == null || vrCameraTransform == null)
        {
            Debug.LogError("VR/UI �]�w������A���ˬd Inspector ���� AudioSource, RuleText, XR Origin, �� VR Camera Transform �O�_�w�]�w�I");
            return;
        }

        // �T�O�@�ɤ�V�P���a HMD ��V�P�B�]�b�C���y�{�}�l�e����^
        ApplyCameraRotationToOrigin();

        // �ҰʥD�n���C���y�{��{
        StartCoroutine(StartGameFlow());
    }

    // =================================================================
    // �֤ߥ\���k
    // =================================================================

    /// <summary>
    /// �N VR ��v���� Y �b�������Ψ� XR Origin�A�H����@�ɰ_�l��V�C
    /// </summary>
    public void ApplyCameraRotationToOrigin()
    {
        // 1. �����v��������
        Quaternion cameraRotation = vrCameraTransform.rotation;

        // 2. �ȴ��� Y �b (Yaw) �����סA�������� (Pitch) �M���� (Roll)
        Vector3 euler = cameraRotation.eulerAngles;

        // 3. �Ыؤ@�ӥu�]�t Y �b���઺�s�|����
        Quaternion targetRotation = Quaternion.Euler(0f, euler.y, 0f);

        // 4. �N�� Y �b�������Ψ� XR Origin�A�o�|������ VR �@��
        xrOrigin.transform.rotation = targetRotation;

        Debug.Log($"VR �Ŷ���V�w��w�CXR Origin ���� Y �b���׬�: {targetRotation.eulerAngles.y}");
    }

    /// <summary>
    /// �C���}���M�оǬy�{�������{�C
    /// </summary>
    IEnumerator StartGameFlow()
    {
        // 1. �C���@�}�l���� initialDelaySeconds ��
        yield return new WaitForSeconds(initialDelaySeconds);

        // ��ܳW�h��r����
        RuleText_rule.gameObject.SetActive(true);

        // 2. ����Ĥ@���q�y���M��r (���� 0, 1, 2)
        // �y��: "�w��Ө�VR�ֶ�"
        // �y��: "�ڭ̷ǳƤF�@�t�C���D�ԥ���"
        // �y��: "���Ȧ��\��i�H��o�_�c���_��"
        for (int i = 0; i < 3; i++)
        {
            if (i < ruleClips.Length)
            {
                yield return StartCoroutine(PlayVoiceAndText(ruleTexts[i], ruleClips[i]));
            }
        }

        // 3. �y������"���Ȧ��\��i�H��o�_�c���_��"��A���� RuleText_rule�A��� treasurebg_rule
        RuleText_rule.gameObject.SetActive(false);
        treasurebg_rule.SetActive(true);

        // 4. ���� treasureDisplaySeconds ��
        yield return new WaitForSeconds(treasureDisplaySeconds);

        // 5. ���� treasurebg_rule�A��� RuleText_rule
        treasurebg_rule.SetActive(false);
        RuleText_rule.gameObject.SetActive(true);

        // 6. �~�򼷩����y���M��r (���� 3)
        // �y��: "�{�b���Ӫ��D�D�Ԫ��W�h"
        if (ruleClips.Length > 3)
        {
            yield return StartCoroutine(PlayVoiceAndText(ruleTexts[3], ruleClips[3]));
        }

        Debug.Log("�}���y�{�����A�C�������}�l�I");
    }

    /// <summary>
    /// ���U��k�G�P�B�����q�y���M��ܤ�r�C
    /// </summary>
    IEnumerator PlayVoiceAndText(string text, AudioClip clip)
    {
        // �g�J��r
        RuleText_rule.text = text;

        // ����y��
        voiceAudioSource.PlayOneShot(clip);

        // ���ݻy�����񧹲�
        yield return new WaitForSeconds(clip.length);
    }
}