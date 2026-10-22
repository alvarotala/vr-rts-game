using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private RectTransform referenceGuide;
        private string referenceFaction;
        private int referenceUnitIndex,referenceStructureIndex;
        private readonly Target[] referenceFactionTabs=new Target[2],referenceEntries=new Target[7];
        private readonly Text[] referenceFactionNames=new Text[2],referenceNames=new Text[7],referenceRoles=new Text[7];
        private readonly RectTransform[] referenceStatCards=new RectTransform[12];
        private readonly Text[] referenceStatLabels=new Text[12],referenceStatValues=new Text[12];
        private Text referenceCount,referenceName,referenceRole,referenceDescription,referenceRequirements,referenceExtraTitle,referenceExtraDetails;
        private SelectionPortrait referencePortrait;
        private bool IsReferencePage=>Page==MenuPage.UnitsGuide||Page==MenuPage.StructuresGuide;

        private void CreateReferenceGuide()
        {
            referenceGuide=Section(root,"Catalog reference",new Vector2(30,-216),new Vector2(1040,600),new Color(.035f,.08f,.12f));
            for(int i=0;i<referenceFactionTabs.Length;i++)
            {
                int index=i;
                var tab=Section(referenceGuide,"Reference faction "+i,new Vector2(16+i*254,-14),new Vector2(244,48));
                referenceFactionNames[i]=Label(tab,"",new Vector2(8,0),new Vector2(228,48),20,Color.white,TextAnchor.MiddleCenter);
                referenceFactionTabs[i]=new Target{rect=tab,image=tab.GetComponent<Image>(),normal=button,click=()=>{
                    referenceFaction=Session.Catalog.Definition.factions[index].id;Rebuild();UpdateMenuText();
                }};
            }
            referenceCount=Label(referenceGuide,"",new Vector2(540,-14),new Vector2(484,48),16,new Color(.68f,.8f,.85f),TextAnchor.MiddleRight);
            for(int i=0;i<referenceEntries.Length;i++)
            {
                int index=i;
                var entry=Section(referenceGuide,"Reference entry "+i,new Vector2(16,-82-i*70),new Vector2(254,62));
                referenceNames[i]=Label(entry,"",new Vector2(12,-6),new Vector2(230,28),20,Color.white);
                referenceRoles[i]=Label(entry,"",new Vector2(12,-34),new Vector2(230,22),15,new Color(.67f,.8f,.85f));
                referenceEntries[i]=new Target{rect=entry,image=entry.GetComponent<Image>(),normal=button,click=()=>{
                    if(Page==MenuPage.StructuresGuide)referenceStructureIndex=index;else referenceUnitIndex=index;
                    Rebuild();UpdateMenuText();
                }};
            }
            var detail=Section(referenceGuide,"Reference detail",new Vector2(286,-82),new Vector2(738,502),new Color(.055f,.12f,.17f));
            referenceName=Label(detail,"",new Vector2(16,-8),new Vector2(572,32),26,Color.white);
            referenceRole=Label(detail,"",new Vector2(16,-42),new Vector2(572,24),18,accent);
            referenceDescription=Label(detail,"",new Vector2(16,-72),new Vector2(572,48),17,new Color(.8f,.87f,.9f),TextAnchor.UpperLeft);
            var portraitObject=new GameObject("Reference model",typeof(RectTransform),typeof(CanvasRenderer),typeof(SelectionPortrait));
            var portraitRect=portraitObject.GetComponent<RectTransform>();portraitRect.SetParent(detail,false);
            TopLeft(portraitRect,new Vector2(600,-8),new Vector2(122,112));
            referencePortrait=portraitObject.GetComponent<SelectionPortrait>();referencePortrait.raycastTarget=false;
            for(int i=0;i<referenceStatCards.Length;i++)
            {
                var card=Section(detail,"Reference stat "+i,new Vector2(16+(i%4)*178,-134-(i/4)*56),new Vector2(170,50),new Color(.035f,.085f,.125f));
                referenceStatCards[i]=card;
                referenceStatLabels[i]=Label(card,"",new Vector2(10,-4),new Vector2(150,18),14,new Color(.65f,.79f,.84f));
                referenceStatValues[i]=Label(card,"",new Vector2(10,-22),new Vector2(150,25),21,Color.white);
            }
            referenceRequirements=Label(detail,"",new Vector2(16,-312),new Vector2(706,49),17,new Color(.78f,.86f,.89f),TextAnchor.UpperLeft);
            referenceExtraTitle=Label(detail,"",new Vector2(16,-369),new Vector2(706,22),15,accent);
            referenceExtraDetails=Label(detail,"",new Vector2(16,-396),new Vector2(706,98),17,new Color(.82f,.89f,.91f),TextAnchor.UpperLeft);
        }

        private void BuildReferenceGuide()
        {
            referenceGuide.gameObject.SetActive(IsReferencePage);if(!IsReferencePage)return;
            var catalog=Session.Catalog;
            if(string.IsNullOrEmpty(referenceFaction))referenceFaction=Session.Faction;
            bool structures=Page==MenuPage.StructuresGuide;
            List<Archetype> entries=CatalogReference.Entries(catalog,referenceFaction,structures);
            int selected=Mathf.Clamp(structures?referenceStructureIndex:referenceUnitIndex,0,entries.Count-1);
            if(structures)referenceStructureIndex=selected;else referenceUnitIndex=selected;
            for(int i=0;i<referenceFactionTabs.Length;i++)
            {
                var faction=catalog.Definition.factions[i];var tab=referenceFactionTabs[i];
                tab.rect.name="Reference faction: "+faction.id;
                referenceFactionNames[i].text=faction.name;
                tab.normal=faction.id==referenceFaction?MenuPalette.Primary:button;tab.image.color=tab.normal;targets.Add(tab);
            }
            referenceCount.text=entries.Count+(structures?" STRUCTURES":" UNITS")+"  ·  BASE STATS";
            for(int i=0;i<referenceEntries.Length;i++)
            {
                bool visible=i<entries.Count;var entry=referenceEntries[i];entry.rect.gameObject.SetActive(visible);if(!visible)continue;
                var type=entries[i];entry.rect.name="Reference entry: "+type.id;
                referenceNames[i].text=type.name;referenceRoles[i].text=CatalogReference.Role(type);
                entry.normal=i==selected?MenuPalette.Primary:button;entry.image.color=entry.normal;targets.Add(entry);
            }
            var current=entries[selected];referenceName.text=current.name;referenceRole.text=CatalogReference.Role(current);
            referencePortrait.SetType(current);referenceDescription.text=CatalogReference.Description(catalog,current);
            var stats=CatalogReference.Stats(current);
            for(int i=0;i<referenceStatCards.Length;i++)
            {
                bool visible=i<stats.Count;referenceStatCards[i].gameObject.SetActive(visible);if(!visible)continue;
                referenceStatCards[i].name="Reference stat: "+stats[i].Label;
                referenceStatLabels[i].text=stats[i].Label;referenceStatValues[i].text=stats[i].Value;
            }
            referenceRequirements.text=CatalogReference.Requirements(catalog,current);
            referenceExtraTitle.text=CatalogReference.ExtraTitle(current);referenceExtraDetails.text=CatalogReference.ExtraDetails(catalog,current);
        }
    }
}
