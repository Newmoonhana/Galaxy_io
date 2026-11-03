using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LoadingCircleAnim : MonoBehaviour
{
    //프레임 교체는 Restart(0↔1 왕복). 회전은 각 이미지가 투명해질 때 콜백으로 누적함
    private Sequence frame_seq;
    private Tween frame01FadeIn_tw, frame01FadeOut_tw, frame02FadeIn_tw, frame02FadeOut_tw;

    //Frame_01 / Frame_02 는 UGUI 라 SpriteRenderer 가 아니라 Image 를 달고 있음
    private Image frame01_img, frame02_img;
    
    private void Awake()
    {
        //오브젝트 연결
        frame01_img = transform.GetChild(0).GetComponent<Image>();
        frame02_img = transform.GetChild(1).GetComponent<Image>();
        
        //DOTween
        const float duration = 0.5f;
        frame01FadeIn_tw = frame01_img.DOFade(0, duration);
        frame02FadeIn_tw = frame02_img.DOFade(0, duration);
        frame01FadeOut_tw = frame01_img.DOFade(1, duration);
        frame02FadeOut_tw = frame02_img.DOFade(1, duration);

        //무한 루프라 스스로 끝나지 않음. 팝업이 사라질 때 같이 죽도록 묶음
        //한 칸(45도)마다 두 프레임을 교차 페이드함
        //Frame_02 는 Frame_01 보다 시계 방향으로 한 칸 앞선 그림이라, 부모를 통째로 돌리면 02→01 로 페이드하는 칸에서 제일 밝은 칸이 뒤로 물러남
        //그래서 부모 대신 투명해진 쪽 이미지만 -90도(두 칸) 돌려 다음 칸에서 앞서는 그림이 되게 함
        frame_seq = DOTween.Sequence().SetLoops(-1, LoopType.Restart).SetUpdate(true)
            .SetAutoKill(true).SetLink(gameObject);
        frame_seq.Append(frame01FadeIn_tw).Join(frame02FadeOut_tw);
        frame_seq.AppendCallback(() => frame01_img.rectTransform.Rotate(0, 0, -90));
        frame_seq.Append(frame01FadeOut_tw).Join(frame02FadeIn_tw);
        frame_seq.AppendCallback(() => frame02_img.rectTransform.Rotate(0, 0, -90));
    }
}
