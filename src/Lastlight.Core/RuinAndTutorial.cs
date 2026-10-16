namespace Lastlight.Core;
public sealed partial class World
{
    private CommandResult ApplySliceCommand(WorldCommand c,Definitions d)
    {
        EnsureSlice(d);
        if(c.Kind==CommandKind.PrepareFormula)return QueueFormula(c,d);
        if(c.Kind==CommandKind.CastFormula)return BeginCast(c,d);
        if(c.Kind==CommandKind.TutorialAction)
        {
            if(c.Text=="dismiss")Slice.Tutorial.Dismissed=c.Quantity!=0;
            else if(c.Text=="music")Slice.MusicEnabled=c.Quantity!=0;
            else if(c.Text=="sound")Slice.SoundEnabled=c.Quantity!=0;
            else return new(false,"Unknown tutorial or audio setting.");
            return new(true,"Setting updated.");
        }
        if(c.Kind!=CommandKind.SliceAction || !Slice.Enabled)return new(false,"Start the three-elevation observatory scenario to explore this ruin.");
        var actor=Find(c.EntityId);var target=Find(c.TargetId);
        if(actor==null || !Colony.People.TryGetValue(c.EntityId,out var profile) || !CanAct(actor.Id) || profile.Drafted)return new(false,"Select a capable undrafted resident for fieldwork.");
        if(target==null || target.Forbidden)return new(false,"Select an allowed ruin object.");
        var ruin=Slice.Ruin;
        string action=c.Text;
        if(action=="survey" && target.Id!=ruin.TabletId)return new(false,"Survey the surface field tablet.");
        if(action=="archive" && target.Id!=ruin.FolioId)return new(false,"Read the keeper's archive folio.");
        if(action=="cache" && (target.Id!=ruin.CacheId || ruin.LensRecovered))return new(false,"This cache has no unrecovered lens.");
        if(action=="engine" && target.Id!=ruin.EngineId)return new(false,"Inspect the daylight engine.");
        if(action=="open" && (target.Id!=ruin.DoorId || !ruin.ArchiveRead))return new(false,"The archive contains the hatch's release sequence.");
        if(action=="repair" && (target.Id!=ruin.EngineId || ruin.EngineRestored || ruin.EngineKnowledge!="Understood" || !ruin.LensRecovered))return new(false,"Understand the fault and recover the memory-glass lens before restoration.");
        if(action is not ("survey" or "archive" or "cache" or "engine" or "repair" or "open"))return new(false,"Unknown fieldwork action.");
        int cell=Index(target.X,target.Y,target.Z);if(FindPath(actor,profile,cell,true,d)==null)return new(false,"The object is unreachable. Check stair links, bridges, doors and allowed areas.");
        if(Autonomy.Claims.ContainsKey("ruin:"+target.Id))return new(false,"Another resident has claimed this fieldwork.");
        var job=new ColonyJob{PawnId=actor.Id,Kind="Ruin",TargetId=target.Id,DefinitionId=action,Cell=cell,Claims=["ruin:"+target.Id]};
        if(action=="repair" && !ReserveIngredients(job,new(){{"lens",1},{"brass",12},{"blocks",10}},d))return new(false,"Restoration needs a reachable lens, 12 sun-brass scraps and 10 basalt blocks.");
        Interrupt(actor.Id,d);Danger.Orders.Remove(actor.Id);if(!Start(job))return new(false,"Fieldwork claims are unavailable.");profile.RetryAt=Tick;return new(true,"Fieldwork ordered; the resident will travel and work at the object.");
    }
    private void CompleteRuinAction(ColonyJob job,Entity actor,Definitions d)
    {
        var ruin=Slice.Ruin;
        switch(job.DefinitionId)
        {
            case "survey":
                ruin.Discovered=true;Slice.Links.First(l=>l.A/(Width*Height)==0).Enabled=true;Slice.Formulae[actor.Id].Known.Add("ember");
                Notice("Discovery","The field tablet identifies a buried observatory and opens its entry stair. The Borrowed Dawn formula is recovered.",job.Cell);break;
            case "archive":
                ruin.ArchiveRead=true;foreach(var f in d.Document.Formulae)Slice.Formulae[actor.Id].Known.Add(f.Id);
                foreach(var link in Slice.Links.Where(l=>l.A/(Width*Height)==1))link.Enabled=true;
                Notice("Discovery","The keeper's folio explains four formulae, the hatch sequence and the engine's cracked lens. The deep stair opens.",job.Cell);break;
            case "cache":
                ruin.CacheSurveyed=true;
                if(!ruin.LensRecovered)
                {
                    Drop("lens",1,Index(actor.X,actor.Y,actor.Z),d);ruin.LensId=Entities.Last(e=>e.DefinitionId=="lens").Id;ruin.LensRecovered=true;
                    Drop("spark",6,Index(actor.X,actor.Y,actor.Z),d);
                    Notice("Discovery","A unique memory-glass lens and six sun-salt reagents are recovered from the seed reliquary.",job.Cell);
                }break;
            case "engine":
                ruin.EngineKnowledge=ruin.ArchiveRead?"Understood":"Observed";
                Notice("Discovery",ruin.ArchiveRead?"The archive's diagram reveals the engine's cracked lens and unstable light coupling.":"The engine is observed, but its fault cannot yet be interpreted.",job.Cell);break;
            case "open":
                var door=Find(ruin.DoorId);if(door!=null){Entities.Remove(door);RemoveSliceEntity(door.Id,d);}ruin.DoorId=0;
                Notice("Discovery","The hatch opens safely. The machine hall's stores are accessible.",job.Cell);break;
            case "repair":
                ruin.EngineRestored=true;ruin.EngineKnowledge="Understood";ruin.WardWarning=true;ruin.WardAt=Tick+TicksPerHour;
                Slice.Tutorial.Completed.Add("restoration");Danger.PauseSerial++;
                Notice("Restoration","The daylight engine wakes. The deep garden now has usable light and warmth; an ancient warder will awaken in one game hour.",job.Cell);break;
        }
        Remember(actor.Id,"Discovery","I recovered knowledge from the buried observatory.",7,job.TargetId,5);
    }
    private void AdvanceRuin(Definitions d)
    {
        var ruin=Slice.Ruin;
        if(ruin.WardWarning && Tick>=ruin.WardAt)
        {
            ruin.WardWarning=false;var engine=Find(ruin.EngineId);if(engine==null)return;
            var cell=HorizontalNeighbors(Index(engine.X,engine.Y,engine.Z)).Where(i=>Traversable(i,d)).LastOrDefault(-1);if(cell<0)return;
            var at=Position(cell);var result=Apply(new(CommandKind.Spawn,DefinitionId:"vault-guardian",X:at.X,Y:at.Y,Z:at.Z),d);
            if(result.Accepted){ruin.GuardianId=result.EntityId;Slice.Hostiles.Add(result.EntityId);Body(result.EntityId)!.WeaponId="guardian-focus";Danger.PauseSerial++;Notice("Threat","The observatory warder awakens to defend its engine.",cell);}
        }
        if(ruin.GuardianId!=0 && Body(ruin.GuardianId) is { } body && (body.Dead || body.Downed) && !ruin.DefenseComplete)
        {
            ruin.DefenseComplete=true;body.Captured=!body.Dead;Slice.Hostiles.Remove(ruin.GuardianId);Danger.Orders.Remove(ruin.GuardianId);Danger.PauseSerial++;
            foreach(long id in Colony.People.Keys)if(Body(id) is {Dead:false})Remember(id,"ObservatoryDefense","We defended the restored observatory.",8,ruin.GuardianId,6);
            Notice("Defense","The warder is disabled. The refuge keeps the engine and its recovered knowledge.",Index(Find(ruin.EngineId)!.X,Find(ruin.EngineId)!.Y,Find(ruin.EngineId)!.Z));
        }
    }
    private void AdvanceRuinGuardians(Definitions d)
    {
        foreach(long id in Slice.Hostiles.Order().ToArray())
        {
            var actor=Find(id);if(actor==null || !Hostile(id))continue;
            var target=Entities.Where(e=>Colony.People.ContainsKey(e.Id) && CanAct(e.Id) && (Distance(actor,e)<=12 || e.Z!=actor.Z)).OrderBy(e=>e.Z==actor.Z?0:1).ThenBy(e=>e.Z==actor.Z?Distance(actor,e):0).ThenBy(e=>e.Id).FirstOrDefault();
            if(target==null)continue;if(!Danger.Orders.TryGetValue(id,out var order))Danger.Orders[id]=order=new();order.Kind="Attack";order.TargetId=target.Id;
            if(!TryAttack(id,target.Id,d) && (Distance(actor,target)>Weapon(id,d).Range || !HasLineOfSight(actor,target,d)))
            {order.Path.Clear();Pursue(actor,Index(target.X,target.Y,target.Z),true,order,d);}
        }
    }
    public static readonly (string Id,string Title,string Hint)[] TutorialSteps=
    [
        ("food","Keep a food reserve","Let the existing stove bill and growing zone work. Inspect priorities and Needs if supplies stall."),
        ("discovery","Survey the observatory","Select an undrafted scholar and use Ruin > Survey field tablet. The surface stair then opens."),
        ("crossing","Bridge the archive rift","Go to Observatory vault with ]. Architect > Floors > Bridge floor; fill the three central gap tiles. Levels previews support."),
        ("archive","Read the keeper's folio","Ruin > Read archive. Recover the formulae and the deep-stair release sequence."),
        ("preparation","Prepare a formula","Select the scholar, enable Research work, then Formulae > Prepare. A study desk and physical reagent are required."),
        ("lens","Recover the unique lens","Descend to Deep archive and survey the seed reliquary. The recovered lens remains a physical item."),
        ("restoration","Restore the daylight engine","Inspect the engine, then restore it with the lens, 12 brass and 10 basalt blocks. The warder warning gives preparation time."),
        ("magic","Use a prepared formula","Draft the caster; choose a prepared formula, then click its target. The preparation is spent when channeling starts."),
        ("defense","Defend the restored observatory","Equip bows, arrows or melee weapons. Draft defenders, then attack/hold. Field medicine and Returning Flesh support recovery."),
        ("adaptation","Harvest in the deep garden","Draw a growing zone on deep soil. Restored light allows duskgrain to mature underground; fungus needs less light.")
    ];
    private void AdvanceTutorial(Definitions d)
    {
        if(!Slice.Tutorial.Enabled)return;var tutorial=Slice.Tutorial;
        void Done(string id,bool state){if(state && tutorial.Completed.Add(id))Notice("Objective","Completed: "+TutorialSteps.First(s=>s.Id==id).Title+".",Slice.Ruin.GardenCell>=0?Slice.Ruin.GardenCell:0);}
        Done("food",Resources("meal").Total>=5 || Autonomy.Harvested>0);Done("discovery",Slice.Ruin.Discovered);
        Done("crossing",Slice.BridgeFloors.Count>=3);Done("archive",Slice.Ruin.ArchiveRead);Done("preparation",Slice.Formulae.Values.Any(p=>p.Prepared.Count>0));
        Done("lens",Slice.Ruin.LensRecovered);Done("restoration",Slice.Ruin.EngineRestored);Done("magic",Slice.CompletedCasts>0);Done("defense",Slice.Ruin.DefenseComplete);
        if(tutorial.Completed.Count>=TutorialSteps.Length && tutorial.CompletedAt==0){tutorial.CompletedAt=Tick;Danger.PauseSerial++;Notice("Milestone","The observatory vertical slice is complete. Continue growing, building and caring for the refuge.",Slice.Ruin.GardenCell);}
    }
}
