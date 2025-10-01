using UnityEngine;
using System.Collections;
using TMPro; // �O�o�ޤJ TextMeshPro �R�W�Ŷ�

public class Rule_script : MonoBehaviour
{
    // === ���}�ܼơG�b Inspector ���즲�]�w ===

    [Header("UI & 3D ����")]
    [Tooltip("�Ω���ܳW�h��r�� TextMeshPro ����")]
    public TextMeshPro RuleText_rule;

    [Tooltip("�b�W�h�������q�n��ܪ��I��/���ܪ���")]
    public GameObject treasurebg_rule;

    [Header("�y���]�w")]
    [Tooltip("����y���Ϊ� AudioSource ����")]
    public AudioSource voiceAudioSource;

    [Tooltip("�C�q�y���ɪ� Clip (�зӶ��ǩ즲)")]
    public AudioClip[] ruleClips;

    [Header("�ɶ��]�w")]
    [Tooltip("�C���}�l�ɪ���l������")]
    public float initialDelaySeconds = 3f;

    [Tooltip("�b 'treasurebg_rule' ��ܫᵥ�ݪ����")]
    public float treasureDisplaySeconds = 3f;

    // === �W�h��r�}�C (�P�y�����ǹ���) ===
    private string[] ruleTexts = new string[]
    {
        "�w��Ө�VR�ֶ�",
        "�ڭ̷ǳƤF�@�t�C���D�ԥ���",
        "���Ȧ��\��i�H��o�_�c���_��",
        "�{�b���Ӫ��D�D�Ԫ��W�h"
    };

    // === Unity �ͩR�g����k ===

    void Start()
    {
        // �T�O RuleText �M treasurebg_rule �@�}�l�O���ê�
        RuleText_rule.gameObject.SetActive(false);
        treasurebg_rule.SetActive(false);

        // �ˬd�]�w�O�_����
        if (voiceAudioSource == null || RuleText_rule == null)
        {
            Debug.LogError("�Цb Inspector ���즲�]�w voiceAudioSource �M RuleText_rule!");
            return;
        }

        // �ҰʥD�n���C���y�{��{
        StartCoroutine(StartGameFlow());
    }

    // === �C���y�{�����{ ===

    IEnumerator StartGameFlow()
    {
        // 1. �C���@�}�l���� initialDelaySeconds ��
        yield return new WaitForSeconds(initialDelaySeconds);

        // ��� RuleText_rule ����
        RuleText_rule.gameObject.SetActive(true);

        // 2. ����Ĥ@���q�y���M��r (���� 0, 1)
        // �q�Ĥ@�q�y���}�l����
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
        if (ruleClips.Length > 3)
        {
            yield return StartCoroutine(PlayVoiceAndText(ruleTexts[3], ruleClips[3]));
        }

        Debug.Log("�}���y�{�����A�i�H�}�l�C���ΤU�@�Ӭy�{�C");
    }

    // === ���U��k�G�P�B����y���M��r ===

    IEnumerator PlayVoiceAndText(string text, AudioClip clip)
    {
        // �g�J��r
        RuleText_rule.text = text;

        // ����y��
        voiceAudioSource.PlayOneShot(clip);

        // ���ݻy�����񧹲� (�y�������״N�O���ݪ��ɶ�)
        yield return new WaitForSeconds(clip.length);
    }
}