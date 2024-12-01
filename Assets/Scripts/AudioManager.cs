using System.Collections.Generic;
using System.Threading.Tasks;
using Singleton;
using UnityEngine;

public class AudioManager : Singleton<AudioManager>
{
    private const int HEADER_SIZE = 44;
    
    [SerializeField] private AudioSource  goSpeakerOne;
    [SerializeField] private AudioSource  goSpeakerTwo;
    [SerializeField] private AudioSource  goSpeakerThree;

    private List<AudioSource> goSpeakers = new List<AudioSource>();
        
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        goSpeakers.Add(goSpeakerOne);
        goSpeakers.Add(goSpeakerTwo);
        goSpeakers.Add(goSpeakerThree);
    }

    public void ConvertAndPlay(byte[] audioData)
    {
        int idxRandomPlay = Random.Range(0, 3);
        
        // WAV 헤더를 제외한 PCM 데이터만 추출
        byte[] pcmData = new byte[audioData.Length - HEADER_SIZE];
        System.Array.Copy(audioData, HEADER_SIZE, pcmData, 0, pcmData.Length);

        // PCM 데이터를 float 배열로 변환
        float[] floatData = new float[pcmData.Length / 2];
        for (int i = 0; i < floatData.Length; i++)
        {
            short sample = System.BitConverter.ToInt16(pcmData, i * 2);
            floatData[i] = sample / 32768.0f; // Int16 최대값으로 정규화
        }
        
        // AudioClip 생성 (샘플링 속도, 채널 수는 WAV 파일에 맞게 설정 필요)
        AudioClip audioClip = AudioClip.Create("MyAudioClip", floatData.Length, 1, 24000, false);
        audioClip.SetData(floatData, 0);
        
        Debug.Log(Time.time + $" Speaker {idxRandomPlay}에서 플레이!");
        foreach (var audioSource in goSpeakers)
        {
            audioSource.GetComponent<SpriteRenderer>().color = Color.white;
        }
        goSpeakers[idxRandomPlay].GetComponent<SpriteRenderer>().color = Color.cyan;
        goSpeakers[idxRandomPlay].PlayOneShot(audioClip);
    }
}
