using Script.Framework.UI;
using Script.UI.Components;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class ImageInfoPanel : BasePanel
    {
        [SerializeField] private RectTransform Panel;
        [SerializeField] private ImageLoadComp TemplateImage;

        [SerializeField] private Text Size;


        public override void SetData(object data)
        {
            _useScaleAnim = false;

            string path = data as string;
            TemplateImage.SetData(path, new Vector2(1700,800), false);
            var size = TemplateImage.GetSize();
            var target = size + new Vector2(150, 220);

            target = new Vector2(Mathf.Max(target.x, 480), Mathf.Max(target.y, 500));
            Panel.sizeDelta = target;
            Size.text = string.Format("{0} X {1}", (int)size.x, (int)size.y);
        }
    }
}