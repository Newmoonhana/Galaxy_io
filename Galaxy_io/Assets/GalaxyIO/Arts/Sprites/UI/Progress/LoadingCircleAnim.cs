using System;
using DG.Tweening;
using UnityEngine;

public class LoadingCircleAnim : MonoBehaviour
{
    private Sequence loading_seq;
    private Tween rotate_tw, frame01FadeIn_tw, frame01FadeOut_tw, frame02FadeIn_tw, frame02FadeOut_tw;

    private SpriteRenderer frame01_sr, frame02_sr;
    
    private void Awake()
    {
        //오브젝트 연결
        frame01_sr = transform.GetChild(0).GetComponent<SpriteRenderer>();
        frame02_sr = transform.GetChild(1).GetComponent<SpriteRenderer>();
        
        //DOTween
        const float duration = 0.04f;
        loading_seq = DOTween.Sequence().SetLoops(-1, LoopType.Incremental).SetUpdate(true).SetAutoKill(true);
        rotate_tw = transform.DOLocalRotate(new Vector3(0, 0, -45), 0, RotateMode.FastBeyond360);
        frame01FadeIn_tw = frame01_sr.DOFade(0, duration);
        frame02FadeIn_tw = frame02_sr.DOFade(0, duration);
        frame01FadeOut_tw = frame01_sr.DOFade(1, duration);
        frame02FadeOut_tw = frame02_sr.DOFade(1, duration);
        
        loading_seq.Append(rotate_tw).Join(frame01FadeIn_tw).Join(frame02FadeOut_tw).SetDelay(duration);
        loading_seq.Append(rotate_tw).Join(frame01FadeOut_tw).Join(frame02FadeIn_tw).SetDelay(duration);
    }
}
