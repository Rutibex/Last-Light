namespace Lastlight.Core;
public sealed partial class World
{
    public void ValidateSlice(Definitions d)
    {
        var s=Slice;
        if(s==null || !s.Initialized || s.MagicRandomState==0 || s.NextId<1 || s.Links==null || s.Roofs==null || s.BridgeFloors==null || s.UnstableSince==null || s.Debris==null || s.Buried==null || s.Formulae==null || s.Casts==null || s.Effects==null || s.Ruin==null || s.Tutorial==null || s.Notices==null || s.Hostiles==null)throw new InvalidDataException("Missing vertical slice state.");
        bool Cell(int cell)=>cell>=0 && cell<Cells.Count;
        if(s.Enabled && Levels is <3 or >8 || s.Links.Count>1000 || s.Notices.Count>128 || s.Effects.Count>256 || s.CompletedCasts<0 || s.FailedCasts<0 || s.PorterDeliveries<0 || s.Collapses<0)throw new InvalidDataException("Invalid slice capacity or levels.");
        foreach(var link in s.Links)if(link==null || !Cell(link.A) || !Cell(link.B) || Math.Abs(link.A-link.B)!=Width*Height || Find(link.OwnerId) is not {DefinitionId:"stairs"} owner || Index(owner.X,owner.Y,owner.Z)!=link.A)throw new InvalidDataException("Invalid stair link.");
        if(s.Links.Select(l=>l.OwnerId).Distinct().Count()!=s.Links.Count || s.Roofs.Concat(s.BridgeFloors).Concat(s.Debris).Any(i=>!Cell(i)) || s.UnstableSince.Any(p=>!Cell(p.Key) || p.Value<0 || p.Value>Tick) || s.Buried.Any(id=>Find(id)==null) || s.Hostiles.Any(id=>Find(id)==null))throw new InvalidDataException("Invalid support, debris or hostile state.");
        var ids=new HashSet<long>();void Id(long id){if(id<1 || id>=s.NextId || !ids.Add(id))throw new InvalidDataException("Invalid or duplicate formula/notice identity.");}
        foreach(var pair in s.Formulae)
        {
            var p=pair.Value;
            if(p==null || p.EntityId!=pair.Key || !Colony.People.ContainsKey(pair.Key) || p.Skill is <0 or >20 || !double.IsFinite(p.Strain) || p.Strain is <0 or >100 || p.Known==null || p.Queue==null || p.Prepared==null || p.Queue.Count>8 || p.Prepared.Count>8 || p.Known.Any(k=>!d.Document.Formulae.Any(f=>f.Id==k)) || p.Queue.Any(k=>!p.Known.Contains(k)) || p.Queue.Distinct().Count()!=p.Queue.Count)throw new InvalidDataException("Invalid learned formulae or preparation queue.");
            foreach(var prepared in p.Prepared){if(prepared==null || !p.Known.Contains(prepared.FormulaId) || prepared.PreparedAt<0 || prepared.PreparedAt>Tick)throw new InvalidDataException("Invalid prepared formula.");Id(prepared.Id);}
            if(MemoryUsed(pair.Key,d)>p.Capacity)throw new InvalidDataException("Prepared formula memory exceeded.");
        }
        if(s.Formulae.Count!=Colony.People.Count)throw new InvalidDataException("Missing formula profile.");
        foreach(var c in s.Casts)if(c.Value==null || c.Key!=c.Value.ActorId || !Colony.People.ContainsKey(c.Key) || !d.Document.Formulae.Any(f=>f.Id==c.Value.FormulaId) || !Cell(c.Value.Cell) || !Cell(c.Value.OriginCell) || c.Value.TargetId<0 || c.Value.TargetId>=NextEntityId || c.Value.FinishAt<0 || !double.IsFinite(c.Value.FailureChance) || c.Value.FailureChance is <0 or >1)throw new InvalidDataException("Invalid formula channel.");
        foreach(var e in s.Effects)
        {
            if(e==null || !Colony.People.ContainsKey(e.ActorId) || !Cell(e.Cell) || e.Kind is not ("Light" or "Seal" or "Porter") || e.Radius is <0 or >8 || e.ExpiresAt<0 || e.CargoCount is <0 or >15 || e.CargoCount>0 && (!d.Entities.TryGetValue(e.CargoId,out var item) || item.Kind!=EntityKind.ItemStack) || e.Path==null || e.Path.Any(i=>!Cell(i)) || e.Claims==null || e.Claims.Any(k=>Autonomy.Claims.GetValueOrDefault(k)!=e.ActorId) || e.Kind=="Porter" && !Cell(e.Destination))throw new InvalidDataException("Invalid magical effect or porter cargo.");Id(e.Id);
        }
        foreach(var n in s.Notices){if(n==null || n.Tick<0 || n.Tick>Tick || !Cell(n.Cell) || n.Text==null || n.Text.Length>512)throw new InvalidDataException("Invalid slice notice.");Id(n.Id);}
        var r=s.Ruin;
        if(r.EngineKnowledge is not ("Unknown" or "Observed" or "Identified" or "Understood") || r.GardenCell < -1 || r.GardenCell>=Cells.Count || r.WardAt<0 || new[]{r.TabletId,r.FolioId,r.CacheId,r.DoorId,r.EngineId,r.GuardianId,r.LensId}.Any(id=>id<0 || id>=NextEntityId))throw new InvalidDataException("Invalid ruin knowledge/reference.");
        if(s.Tutorial.Completed==null || s.Tutorial.Completed.Any(k=>!TutorialSteps.Any(t=>t.Id==k)) || s.Tutorial.StartedAt<0 || s.Tutorial.StartedAt>Tick || s.Tutorial.CompletedAt<0 || s.Tutorial.CompletedAt>Tick)throw new InvalidDataException("Invalid tutorial progression.");
    }
}
