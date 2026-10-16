namespace Lastlight.Core;
public static class VerticalWorldGenerator
{
    public static World Create(Definitions d,int seed,int size=128)
    {
        var w=WorldGenerator.Create(d,seed,size,size);int cx=size/2,cy=size/2;w.Levels=3;
        for(int z=1;z<3;z++)for(int y=0;y<size;y++)for(int x=0;x<size;x++)w.Cells.Add(new(){TerrainId="stone",Variant=(byte)((x*13+y*7+z+seed)%8)});
        w.Slice.Enabled=true;w.Slice.Tutorial.StartedAt=w.Tick;
        void Floor(int x,int y,int z,string id="floor"){w.CellAt(x,y,z).TerrainId=id;w.Slice.Roofs.Add(w.Index(x,y,z));}
        void Room(int x1,int y1,int x2,int y2,int z){for(int y=y1;y<=y2;y++)for(int x=x1;x<=x2;x++)Floor(x,y,z);}
        Room(cx+6,cy-8,cx+12,cy-2,1);Room(cx-5,cy-8,cx+1,cy-2,1);
        Room(cx+6,cy+1,cx+14,cy+9,1);
        for(int x=cx+1;x<=cx+6;x++)for(int y=cy-6;y<=cy-4;y++)Floor(x,y,1);
        for(int y=cy-2;y<=cy+1;y++)for(int x=cx+8;x<=cx+10;x++)Floor(x,y,1);
        for(int x=cx+2;x<=cx+4;x++)for(int y=cy-6;y<=cy-4;y++){w.CellAt(x,y,1).TerrainId="void";w.Slice.Roofs.Remove(w.Index(x,y,1));}
        Room(cx-7,cy-9,cx+4,cy+1,2);
        for(int y=cy-2;y<=cy+1;y++)for(int x=cx-5;x<=cx;x++)Floor(x,y,2,"soil");
        long Spawn(string id,int x,int y,int z,int count=1)
        {var result=w.Apply(new(CommandKind.Spawn,DefinitionId:id,X:x,Y:y,Z:z,Quantity:count),d);if(!result.Accepted)throw new InvalidDataException(result.Message);return result.EntityId;}
        Spawn("column",cx+10,cy+5,1);Spawn("column",cx-2,cy-4,2);Spawn("column",cx+2,cy-1,2);
        var relic=w.Entities.First(e=>e.DefinitionId=="relic");relic.X=cx+2;relic.Y=cy-5;relic.Z=2;
        var ruin=w.Slice.Ruin;ruin.EngineId=relic.Id;ruin.TabletId=Spawn("observatory-tablet",cx+9,cy-7,0);
        ruin.FolioId=Spawn("formula-folio",cx-4,cy-5,1);ruin.CacheId=Spawn("seed-cache",cx-5,cy-7,2);
        ruin.DoorId=Spawn("sealed-door",cx+9,cy,1);
        // The sealed hatch is an ordinary closed door until the archive's release sequence is recovered.
        long entry=Spawn("stairs",cx+9,cy-5,0);w.Slice.Links.Add(new(){OwnerId=entry,A=w.Index(cx+9,cy-5,0),B=w.Index(cx+9,cy-5,1),Enabled=false});
        long deep=Spawn("stairs",cx-2,cy-6,1);w.Slice.Links.Add(new(){OwnerId=deep,A=w.Index(cx-2,cy-6,1),B=w.Index(cx-2,cy-6,2),Enabled=false});
        ruin.GardenCell=w.Index(cx-4,cy,2);
        w.SetupSurvivalCamp(d);w.SetupDangerSupplies(d);
        w.Colony.Blueprints.RemoveAll(b=>b.BuildableId is "stove" or "bed");
        long stove=Spawn("stove",cx-5,cy-3,0);Spawn("study",cx-7,cy+1,0);long bed=Spawn("bed",cx-8,cy-3,0);w.Danger.MedicalBeds.Add(bed);
        w.Apply(new(CommandKind.EditBill,stove,"simple-meal",Text:"add"),d);
        foreach(var supply in new[]{("wood",75), ("blocks",75),("spark",20),("grain",75),("brass",40)})Spawn(supply.Item1,cx-3,cy+3,0,supply.Item2);
        var ids=w.Colony.People.Keys.ToArray();w.EnsureSlice(d);w.Slice.Formulae[ids[0]].Skill=10;
        w.Colony.People[ids[0]].Priorities[15]=1;w.Colony.People[ids[1]].Priorities[6]=1;w.Colony.People[ids[2]].Priorities[9]=1;w.Colony.People[ids[3]].Priorities[8]=1;w.Colony.People[ids[4]].Priorities[2]=1;
        w.Danger.RaidsEnabled=false;w.Events.Clear();w.Record("The refugees establish a camp above a sealed observatory. Three elevations await discovery.");
        w.Validate(d);return w;
    }
}
