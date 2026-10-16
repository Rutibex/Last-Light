namespace Lastlight.Core;
public sealed partial class World
{
    public int MemoryUsed(long actor,Definitions d)=>Slice.Formulae[actor].Prepared.Sum(p=>d.Document.Formulae.First(f=>f.Id==p.FormulaId).MemoryCost);
    public double CastFailure(long actor)=>Math.Clamp(.06+Slice.Formulae[actor].Strain/350-Slice.Formulae[actor].Skill*.003,.02,.4);
    private void CancelChannel(long actor)
    {if(Slice.Casts.Remove(actor))Notice("Interrupted","A formula channel was interrupted; its preparation is spent.",Find(actor) is { } e?Index(e.X,e.Y,e.Z):0);}
    private ColonyJob? PickFormulaPreparation(Entity actor,PawnProfile profile,Definitions d)
    {
        if(!Slice.Enabled || profile.Drafted || !CanAct(actor.Id))return null;
        var magic=Slice.Formulae[actor.Id];
        var policy=Colony.Policies.First(p=>p.Id==profile.Policies["Formulae"]);
        if(policy.Values.GetValueOrDefault("Auto preparation")>0)
            foreach(string known in magic.Known.Order())if(!magic.Prepared.Any(p=>p.FormulaId==known) && !magic.Queue.Contains(known))magic.Queue.Add(known);
        foreach(string requested in magic.Queue.ToArray())
        {
            var formula=d.Document.Formulae.First(f=>f.Id==requested);
            if(MemoryUsed(actor.Id,d)+formula.MemoryCost>magic.Capacity)continue;
            foreach(var bench in Entities.Where(e=>e.DefinitionId=="study" && !e.Forbidden && !Autonomy.Claims.ContainsKey("bench:"+e.Id)).OrderBy(e=>Distance(e,actor)).ThenBy(e=>e.Id))
            {
                int cell=Index(bench.X,bench.Y,bench.Z);if(FindPath(actor,profile,cell,true,d)==null)continue;
                var job=new ColonyJob{PawnId=actor.Id,Kind="Prepare",TargetId=bench.Id,DefinitionId=requested,Cell=cell,Claims=["bench:"+bench.Id]};
                if(ReserveIngredients(job,formula.Reagents,d))return job;
            }
        }
        return null;
    }
    private bool ValidSliceJob(ColonyJob job,PawnProfile profile)
    {
        if(profile.Drafted || job.Sources.Any(id=>Find(id)==null || Find(id)!.Forbidden))return false;
        if(job.Kind=="Prepare")return profile.Priorities[15]>0 && Slice.Formulae[job.PawnId].Queue.Contains(job.DefinitionId) && Find(job.TargetId) is {Forbidden:false,DefinitionId:"study"};
        return job.Kind=="Ruin" && Find(job.TargetId)!=null;
    }
    private bool HandleSliceJob(ColonyJob job,Entity actor,Definitions d)
    {
        if(job.Kind is not ("Prepare" or "Ruin"))return false;
        job.WorkCredit+=Capacity(actor.Id).Manipulation;if(job.WorkCredit<1)return true;job.WorkCredit-=1;job.Work++;
        int needed=job.Kind=="Prepare"?d.Document.Formulae.First(f=>f.Id==job.DefinitionId).PreparationWork:job.DefinitionId=="repair"?120:30;
        if(job.Work<needed)return true;
        if(job.Kind=="Prepare")
        {
            var magic=Slice.Formulae[actor.Id];var formula=d.Document.Formulae.First(f=>f.Id==job.DefinitionId);
            if(MemoryUsed(actor.Id,d)+formula.MemoryCost>magic.Capacity){Interrupt(actor.Id,d);return true;}
            magic.Prepared.Add(new(){Id=Slice.NextId++,FormulaId=job.DefinitionId,PreparedAt=Tick});magic.Queue.Remove(job.DefinitionId);job.Cargo.Clear();
            Notice("Prepared",actor.Name+" prepared "+formula.Name+".",Index(actor.X,actor.Y,actor.Z));
        }
        else CompleteRuinAction(job,actor,d);
        job.Cargo.Clear();Interrupt(actor.Id,d);return true;
    }
    private CommandResult QueueFormula(WorldCommand c,Definitions d)
    {
        if(!Colony.People.TryGetValue(c.EntityId,out var profile) || !CanAct(c.EntityId))return new(false,"Select a capable resident.");
        var magic=Slice.Formulae[c.EntityId];var formula=d.Document.Formulae.FirstOrDefault(f=>f.Id==c.DefinitionId);
        if(formula==null || !magic.Known.Contains(formula.Id))return new(false,"Learn the formula from the observatory first.");
        if(c.Text=="cancel")
        {magic.Queue.Remove(formula.Id);foreach(var job in Autonomy.Jobs.Where(j=>j.PawnId==c.EntityId && j.Kind=="Prepare" && j.DefinitionId==formula.Id).ToArray())Interrupt(c.EntityId,d);return new(true,"Preparation request cancelled; carried reagents returned.");}
        if(profile.Drafted)return new(false,"Undraft before preparing formulae.");
        if(profile.Priorities[15]==0)return new(false,"Enable Research work for preparation.");
        if(magic.Queue.Count>=8 || magic.Queue.Contains(formula.Id))return new(false,"The formula is already queued or the queue is full.");
        magic.Queue.Add(formula.Id);profile.RetryAt=Tick;return new(true,"Formula queued for a study desk.");
    }
    private CommandResult BeginCast(WorldCommand c,Definitions d)
    {
        if(!Colony.People.TryGetValue(c.EntityId,out var profile) || !profile.Drafted || !CanAct(c.EntityId))return new(false,"Select a capable drafted caster.");
        var actor=Find(c.EntityId)!;var magic=Slice.Formulae[c.EntityId];var formula=d.Document.Formulae.FirstOrDefault(f=>f.Id==c.DefinitionId);
        var prepared=magic.Prepared.FirstOrDefault(p=>p.FormulaId==c.DefinitionId);
        if(formula==null || prepared==null)return new(false,"No prepared instance of that formula remains.");
        if(Slice.Casts.ContainsKey(actor.Id))return new(false,"This caster is already channeling.");
        var target=Find(c.TargetId);int cell=target==null?(Contains(c.X,c.Y,c.Z)?Index(c.X,c.Y,c.Z):-1):Index(target.X,target.Y,target.Z);
        if(cell<0)return new(false,"Select a valid target.");var pos=Position(cell);
        if(actor.Z!=pos.Z || Math.Abs(actor.X-pos.X)+Math.Abs(actor.Y-pos.Y)>formula.Range)return new(false,"Target is outside the formula's range or elevation.");
        if(!HasLineOfSight(actor,new Entity{X=pos.X,Y=pos.Y,Z=pos.Z},d))return new(false,"The target is out of sight.");
        var policy=Colony.Policies.First(p=>p.Id==profile.Policies["Formulae"]);
        if(magic.Strain+formula.Strain>policy.Values.GetValueOrDefault("Strain limit",50))return new(false,"The formula would exceed the assigned strain limit.");
        if(formula.Effect=="Seal" && !Entities.Any(e=>e.DefinitionId is "door" or "sealed-door" && Index(e.X,e.Y,e.Z)==cell))return new(false,"The Patient Door requires a doorway.");
        if(formula.Effect=="Flesh" && (target==null || !Colony.People.ContainsKey(target.Id) || IsAway(target.Id) || Body(target.Id) is not {Dead:false}))return new(false,"Returning Flesh requires a living resident; it cannot resurrect.");
        if(formula.Effect=="Porter" && (target==null || d.Entities[target.DefinitionId].Kind!=EntityKind.ItemStack || target.Forbidden || Autonomy.Claims.ContainsKey("item:"+target.Id)))return new(false,"Select an allowed, unclaimed item stack.");
        if(formula.Effect=="Light" && !d.Terrain[Cells[cell].TerrainId].Walkable)return new(false,"The ember needs a clear landing.");
        if(formula.Effect=="Porter" && PorterDestination(target!,actor,d)<0)return new(false,"No reachable eligible stockpile destination.");
        Interrupt(actor.Id,d);Danger.Orders.Remove(actor.Id);magic.Prepared.Remove(prepared);magic.Strain=Math.Min(100,magic.Strain+formula.Strain);
        Slice.Casts[actor.Id]=new(){ActorId=actor.Id,FormulaId=formula.Id,Cell=cell,TargetId=c.TargetId,OriginCell=Index(actor.X,actor.Y,actor.Z),FailureChance=CastFailure(actor.Id),FinishAt=Tick+formula.CastTicks};
        profile.Activity="Channeling "+formula.Name;return new(true,"Channel begun. Preparation is expended; interruption will not refund it.");
    }
    private int PorterDestination(Entity item,Entity caster,Definitions d)
    {
        var profile=Colony.People[caster.Id];
        foreach(var zone in Colony.Zones.Where(z=>z.Kind==ZoneKind.Stockpile && ZoneAccepts(z,item)).OrderByDescending(z=>z.Priority).ThenBy(z=>z.Id))
            foreach(int cell in zone.Cells.OrderBy(i=>Math.Abs(Position(i).X-item.X)+Math.Abs(Position(i).Y-item.Y)).ThenBy(i=>i))
                if(cell!=Index(item.X,item.Y,item.Z) && !Autonomy.Claims.ContainsKey("storage:"+cell) && Traversable(cell,d) && FindPath(caster,profile,cell,false,d)!=null)return cell;
        return -1;
    }
    private void FinishCast(FormulaCast cast,Definitions d)
    {
        var actor=Find(cast.ActorId);if(actor==null || !CanAct(cast.ActorId))return;
        var formula=d.Document.Formulae.First(f=>f.Id==cast.FormulaId);var random=new StableRandom(Slice.MagicRandomState);
        bool failed=random.Next(10000)<cast.FailureChance*10000;Slice.MagicRandomState=random.State;
        if(failed){Slice.FailedCasts++;InflictWound(actor.Id,"left-hand","Burn",3,0,d);Notice("Miscast",actor.Name+" lost control of a formula and suffered a small feedback burn.",cast.Cell);return;}
        if((formula.Effect=="Flesh" && Body(cast.TargetId) is not {Dead:false}) || (formula.Effect=="Seal" && !Entities.Any(e=>e.DefinitionId is "door" or "sealed-door" && Index(e.X,e.Y,e.Z)==cast.Cell)) || (formula.Effect=="Porter" && (Find(cast.TargetId) is not {} pending || pending.Forbidden || Autonomy.Claims.ContainsKey("item:"+pending.Id) || PorterDestination(pending,actor,d)<0))){Notice("Fizzle","The target changed during channeling; the preparation is spent.",cast.Cell);Slice.FailedCasts++;return;}
        Slice.CompletedCasts++;
        if(formula.Effect=="Flesh")
        {
            var body=Body(cast.TargetId);if(body is {Dead:false}){body.MagicBleedStopUntil=Tick+formula.DurationTicks;body.RepairBoostUntil=Tick+formula.DurationTicks;Remember(cast.TargetId,"ReturningFlesh","A formula steadied my wounded body.",5,actor.Id);}
        }
        else if(formula.Effect=="Porter")
        {
            var source=Find(cast.TargetId);if(source==null || source.Forbidden || Autonomy.Claims.ContainsKey("item:"+source.Id))return;
            int destination=PorterDestination(source,actor,d);if(destination<0)return;
            var effect=new FormulaEffect{Id=Slice.NextId++,ActorId=actor.Id,Kind="Porter",Cell=Index(actor.X,actor.Y,actor.Z),SourceId=source.Id,Destination=destination,ExpiresAt=Tick+formula.DurationTicks,Claims=["item:"+source.Id,"storage:"+destination]};
            if(!TryClaim(effect.Claims[0],actor.Id) || !TryClaim(effect.Claims[1],actor.Id)){foreach(string claim in effect.Claims)if(Autonomy.Claims.GetValueOrDefault(claim)==actor.Id)Autonomy.Claims.Remove(claim);return;}
            Slice.Effects.Add(effect);
        }
        else Slice.Effects.Add(new(){Id=Slice.NextId++,ActorId=actor.Id,Kind=formula.Effect,Cell=cast.Cell,Radius=formula.Radius,ExpiresAt=Tick+formula.DurationTicks});
        var engine=Find(Slice.Ruin.EngineId);
        if(formula.Effect=="Light" && !Slice.Ruin.EngineRestored && engine!=null && engine.Z==Position(cast.Cell).Z && Math.Abs(engine.X-Position(cast.Cell).X)+Math.Abs(engine.Y-Position(cast.Cell).Y)<=2)
        {InflictWound(actor.Id,"right-hand","Burn",6,0,d);Notice("Feedback","The unstable engine fed back into the ember. Its fault must be repaired.",cast.Cell);}
        Notice("Cast",actor.Name+" cast "+formula.Name+".",cast.Cell);
    }
    private void AdvanceFormulae(Definitions d)
    {
        foreach(var profile in Slice.Formulae.Values)profile.Strain=Math.Max(0,profile.Strain-.2);
        foreach(var cast in Slice.Casts.Values.OrderBy(c=>c.ActorId).ToArray())
        {
            var actor=Find(cast.ActorId);
            if(actor==null || !CanAct(cast.ActorId) || Index(actor.X,actor.Y,actor.Z)!=cast.OriginCell){CancelChannel(cast.ActorId);continue;}
            if(Tick>=cast.FinishAt){Slice.Casts.Remove(cast.ActorId);FinishCast(cast,d);}
        }
        foreach(var effect in Slice.Effects.OrderBy(e=>e.Id).ToArray())
        {
            if(Tick>=effect.ExpiresAt){EndEffect(effect,d);continue;}if(effect.Kind!="Porter")continue;
            var caster=Find(effect.ActorId);if(caster==null){EndEffect(effect,d);continue;}
            var source=Find(effect.SourceId);int target=effect.PickedUp?effect.Destination:source==null?-1:Index(source.X,source.Y,source.Z);
            if(target<0){EndEffect(effect,d);continue;}
            if(effect.Cell==target)
            {
                if(!effect.PickedUp)
                {
                    if(source!.Forbidden){EndEffect(effect,d);continue;}
                    effect.CargoId=source.DefinitionId;effect.CargoCount=Math.Min(15,source.Quantity);source.Quantity-=effect.CargoCount;if(source.Quantity==0)Entities.Remove(source);
                    effect.PickedUp=true;Autonomy.Claims.Remove("item:"+effect.SourceId);effect.Claims.Remove("item:"+effect.SourceId);effect.Path.Clear();
                }
                else{Drop(effect.CargoId,effect.CargoCount,effect.Cell,d);effect.CargoCount=0;Slice.PorterDeliveries++;EndEffect(effect,d);}continue;
            }
            var profile=Colony.People[caster.Id];
            if(effect.Path.Count==0 || !Neighbors(effect.Cell).Contains(effect.Path[0]) || !Passable(effect.Path[0],profile,d))
            {
                var at=Position(effect.Cell);var proxy=new Entity{X=at.X,Y=at.Y,Z=at.Z};var path=FindPath(proxy,profile,target,false,d);
                if(path==null){EndEffect(effect,d);continue;}effect.Path=path;
            }
            if(effect.Path.Count>0){effect.Cell=effect.Path[0];effect.Path.RemoveAt(0);}
        }
    }
    private void EndEffect(FormulaEffect effect,Definitions d)
    {
        if(effect.CargoCount>0){int cell=d.Terrain[Cells[effect.Cell].TerrainId].Walkable?effect.Cell:NearestTerrainCell(effect.Cell,d);Drop(effect.CargoId,effect.CargoCount,cell,d);effect.CargoCount=0;}
        foreach(string claim in effect.Claims)if(Autonomy.Claims.GetValueOrDefault(claim)==effect.ActorId)Autonomy.Claims.Remove(claim);
        Slice.Effects.Remove(effect);
    }
}
