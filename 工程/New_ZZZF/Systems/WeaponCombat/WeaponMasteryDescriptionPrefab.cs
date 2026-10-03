using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace New_ZZZF
{
    // 仅替换技能详情的说明区域，保留等级、perk 树与原有效果栏。
    // 长说明在固定高度内滚动，不会挤压后面的技能树。
    [PrefabExtension("CharacterDeveloper", "descendant::ListPanel[@Id='SkillHowToLearn']")]
    public sealed class WeaponMasteryDescriptionPrefab : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Replace;

        [PrefabExtensionText]
        public string Content() => @"
<Widget Id='SkillHowToLearn' WidthSizePolicy='StretchToParent' HeightSizePolicy='Fixed' SuggestedHeight='150' MarginBottom='5'>
  <Children>
    <ScrollablePanel Id='MasteryScroll' WidthSizePolicy='StretchToParent' HeightSizePolicy='StretchToParent' MarginRight='16' AutoHideScrollBars='true' ClipRect='ClipRect' InnerPanel='ClipRect\DescriptionList' VerticalScrollbar='..\MasteryScrollbar'>
      <Children>
        <Widget Id='ClipRect' WidthSizePolicy='StretchToParent' HeightSizePolicy='StretchToParent' ClipContents='true'>
          <Children>
            <ListPanel Id='DescriptionList' WidthSizePolicy='StretchToParent' HeightSizePolicy='CoverChildren' StackLayout.LayoutMethod='VerticalTopToBottom'>
              <Children>
                <RichTextWidget DataSource='{CurrentSkill}' WidthSizePolicy='StretchToParent' HeightSizePolicy='CoverChildren' Brush='CharacterDeveloper.MainSkill.Description.Text' Text='@HowToLearnTitle' />
                <TextWidget DataSource='{CurrentSkill}' WidthSizePolicy='StretchToParent' HeightSizePolicy='CoverChildren' Brush='CharacterDeveloper.Skill.Stats.Text' Text='@HowToLearnText' MarginBottom='8' />
                <RichTextWidget DataSource='{CurrentSkill}' WidthSizePolicy='StretchToParent' HeightSizePolicy='CoverChildren' Brush='CharacterDeveloper.Skill.Stats.Text' Text='@DescriptionText' />
              </Children>
            </ListPanel>
          </Children>
        </Widget>
      </Children>
    </ScrollablePanel>
    <ScrollbarWidget Id='MasteryScrollbar' WidthSizePolicy='Fixed' HeightSizePolicy='StretchToParent' SuggestedWidth='8' HorizontalAlignment='Right' AlignmentAxis='Vertical' Handle='Handle' IsVisible='false' MaxValue='100' MinValue='0'>
      <Children>
        <Widget WidthSizePolicy='Fixed' HeightSizePolicy='StretchToParent' SuggestedWidth='4' HorizontalAlignment='Center' Sprite='BlankWhiteSquare_9' AlphaFactor='0.2' Color='#5a4033FF' />
        <ImageWidget Id='Handle' WidthSizePolicy='Fixed' HeightSizePolicy='Fixed' SuggestedWidth='8' SuggestedHeight='10' HorizontalAlignment='Center' Brush='FaceGen.Scrollbar.Handle' />
      </Children>
    </ScrollbarWidget>
  </Children>
</Widget>";
    }
}
