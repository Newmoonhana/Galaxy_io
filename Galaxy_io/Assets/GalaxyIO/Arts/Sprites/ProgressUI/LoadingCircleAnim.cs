using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LoadingCircleAnim : MonoBehaviour
{
    //회전은 Incremental(각도 누적), 프레임 교체는 Restart(0↔1 왕복)라 루프 방식이 서로 달라 Sequence 를 나눔
    private Sequence rotate_seq, frame_seq;
    private Tween rotate_tw, frame01FadeIn_tw, frame01FadeOut_tw, frame02FadeIn_tw, frame02FadeOut_tw;

    //Frame_01 / Frame_02 는 UGUI 라 SpriteRenderer 가 아니라 Image 를 달고 있음
    private Image frame01_img, frame02_img;
    
    private void Awake()
    {
        //오브젝트 연결
        frame01_img = transform.GetChild(0).GetComponent<Image>();
        frame02_img = transform.GetChild(1).GetComponent<Image>();
        
        //DOTween
        const float duration = 0.04f;
        rotate_tw = transform.DOLocalRotate(new Vector3(0, 0, -45), 0, RotateMode.FastBeyond360);
        frame01FadeIn_tw = frame01_img.DOFade(0, duration);
        frame02FadeIn_tw = frame02_img.DOFade(0, duration);
        frame01FadeOut_tw = frame01_img.DOFade(1, duration);
        frame02FadeOut_tw = frame02_img.DOFade(1, duration);
        
        //무한 루프라 스스로 끝나지 않음. 팝업이 사라질 때 같이 죽도록 묶음
        //한 칸(45도)씩 끊어 돌림. Incremental 이 루프마다 각도를 더해 줌
        rotate_seq = DOTween.Sequence().SetLoops(-1, LoopType.Incremental).SetUpdate(true)
            .SetAutoKill(true).SetLink(gameObject);
        rotate_seq.Append(rotate_tw).AppendInterval(duration);
        
        //회전 한 칸마다 두 프레임을 교차 페이드함
        frame_seq = DOTween.Sequence().SetLoops(-1, LoopType.Restart).SetUpdate(true)
            .SetAutoKill(true).SetLink(gameObject);
        frame_seq.Append(frame01FadeIn_tw).Join(frame02FadeOut_tw);
        frame_seq.Append(frame01FadeOut_tw).Join(frame02FadeIn_tw);
    }
}
