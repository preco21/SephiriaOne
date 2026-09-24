using System.Reflection;
using SephiriaOne;
int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
object Call(string method,params object[] args)=>typeof(StartingResourceHooks).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
void Enable()=>typeof(StartingResourceHooks).GetProperty("Available")!.SetValue(null,true);
void Reload()=>((System.Collections.IDictionary)typeof(StartingResourceHooks).GetField("states",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!).Clear();
void NewRun(){SaveManager.CurrentRun=new();SaveManager.CurrentRun.SetInt("SaveVersion",1);DungeonManager.Instance.dungeonEnvironment.Clear();Enable();}
PlayerAvatar NewPlayer(){var p=new PlayerAvatar();var s=new PlayerSpawner{PlayerAvatar=p};p.spawner=s;PlayerSpawner.MultiplayerList.Add(s);return p;}
void Reset(){Reload();NewRun();PlayerSpawner.MultiplayerList.Clear();ResourceRuntime.Policy.Clear();ResourceRuntime.Warnings.Clear();}
void Seed(PlayerAvatar p,int amount)=>Call("InitializeMoney",p,amount);
void Dice(PlayerAvatar p,int amount)=>Call("InitializeDice",p,amount);
void Depart(PlayerAvatar p){int amount=(int)Call("PlanDepartureMoney",p,"STARTINGMONEY");if(amount>0)Call("ApplyDepartureMoney",p,amount);}
void Disconnect(PlayerAvatar p)=>Call("BeforeDisconnect",new Mirror.NetworkConnectionToClient{identity=new Mirror.NetworkIdentity{Player=p}});
bool UninstallFails(){try{StartingResourceHooks.Uninstall();return false;}catch(InvalidOperationException){return true;}}
Reset();var p=NewPlayer();p.Bonus=100;ResourceRuntime.Policy[ResourceKind.Leaves]=new(ResourceMode.Set,500);Seed(p,200);Check(p.currentMoney==200,"seed does not advance addon");p.currentMoney-=50;p.currentMoney+=20;ResourceRuntime.Policy[ResourceKind.Leaves]=new(ResourceMode.Set,900);Depart(p);Check(p.currentMoney==470,"frozen target preserves earnings/spend");Depart(p);Check(p.currentMoney==470,"departure once");
Reset();p=NewPlayer();p.Bonus=100;ResourceRuntime.Policy[ResourceKind.Leaves]=new(ResourceMode.Offset,-250);Seed(p,200);p.currentMoney+=30;Depart(p);Check(p.currentMoney==80,"negative allowance uses grants only");
Reset();p=NewPlayer();ResourceRuntime.Policy[ResourceKind.Leaves]=new(ResourceMode.Multiplier,0.5m);Seed(p,101);p.currentMoney-=20;Depart(p);Check(p.currentMoney==81,"invalid exact result restores withheld native seed");Check(ResourceRuntime.Warnings.Count==1,"invalid future setting warning");
Reset();p=NewPlayer();ResourceRuntime.Policy[ResourceKind.Dice]=new(ResourceMode.Offset,2);Dice(p,3);Check(p.rerollDice==5&&p.maxRerollDice==5,"initial dice grants once");p.rerollDice=0;Seed(p,200);p.currentMoney=0;Disconnect(p);var q=NewPlayer();Seed(q,SaveManager.CurrentRun.GetInt("Player0Money"));Dice(q,SaveManager.CurrentRun.GetInt("Player0RerollDice"));Check(q.currentMoney==0&&q.rerollDice==0,"saved zero restored");Check(q.maxRerollDice==5&&StartingResourceHooks.NativeDice(q)==3,"max contribution restored once");
NewRun();p.currentMoney=0;Dice(p,p.maxRerollDice);Check(p.maxRerollDice==5&&p.rerollDice==5,"restart surviving avatar does not stack maximum");p.rerollDice=2;Dice(p,5);Check(p.rerollDice==2,"duplicate initializer no refill");Reload();Dice(p,5);Check(p.rerollDice==2&&p.maxRerollDice==5,"reload marker prevents refill and stacking");
NewRun();ResourceRuntime.Policy.Clear();Dice(p,p.maxRerollDice);Check(p.rerollDice==3&&p.maxRerollDice==3,"reset affects next fresh grant only");
Reset();p=NewPlayer();DungeonManager.Instance.dungeonEnvironment["IsInDungeon"]=1;ResourceRuntime.Policy[ResourceKind.Leaves]=new(ResourceMode.Set,500);Seed(p,200);Check(p.currentMoney==500,"new midrun player receives one total");Depart(p);Check(p.currentMoney==500,"midrun player no extra departure");
Reset();p=NewPlayer();ResourceRuntime.Policy[ResourceKind.Leaves]=new(ResourceMode.Set,20);Seed(p,200);p.currentMoney=5;Disconnect(p);q=NewPlayer();Seed(q,SaveManager.CurrentRun.GetInt("Player0Money"));Check(q.currentMoney==5,"town reconnect preserves spending");q.Bonus=100;Depart(q);Check(q.currentMoney==5,"town reconnect preserves frozen allocation");
Reset();p=NewPlayer();ResourceRuntime.Policy[ResourceKind.Leaves]=new(ResourceMode.Set,20);Seed(p,200);p.currentMoney=5;ResourceRuntime.Policy[ResourceKind.Dice]=new(ResourceMode.Offset,2);Dice(p,3);p.rerollDice=1;StartingResourceHooks.Uninstall();Check(p.currentMoney==185,"unload restores only withheld native seed");Check(p.rerollDice==1&&p.maxRerollDice==3,"unload does not refill dice");
Reset();p=NewPlayer();ResourceRuntime.Policy[ResourceKind.Leaves]=new(ResourceMode.Set,20);p.OnMoneyWrite=_=>throw new Exception("injected after write");Seed(p,200);Check(p.currentMoney==20&&!StartingResourceHooks.Available,"possible write fault contained");p.OnMoneyWrite=null;Seed(p,200);Check(p.currentMoney==20,"failed write is not replayed");
// Unloading must checkpoint current balances, not the original grant amounts.
Reset();
p = NewPlayer();
ResourceRuntime.Policy[ResourceKind.Leaves] = new(ResourceMode.Set, 20);
ResourceRuntime.Policy[ResourceKind.Dice] = new(ResourceMode.Offset, 2);
Seed(p, 200);
Dice(p, 3);
p.currentMoney = 5;
p.rerollDice = 1;
StartingResourceHooks.Uninstall();
Check(SaveManager.CurrentRun.GetInt("Player0Money") == 185, "unload checkpoints refunded native seed and town spending");
Check(SaveManager.CurrentRun.GetInt("Player0RerollDice") == 1, "unload checkpoints spent dice without refill");

// A duplicate boundary must not even re-parse a now-corrupt persisted record.
Reset();
p = NewPlayer();
Seed(p, 200);
p.currentMoney = 5;
SaveManager.CurrentRun.SetString("SephiriaOne.Starting.v1.Player0.Money", "broken");
Seed(p, 200);
Check(p.currentMoney == 5, "duplicate seed cannot replay through a corrupt checkpoint fallback");

// Native fallback is also a once-only outcome when metadata cannot be trusted.
Reset();
p = NewPlayer();
SaveManager.CurrentRun.SetInt("Player0Money", 200);
SaveManager.CurrentRun.SetString("SephiriaOne.Starting.v1.Player0.Money", "broken");
Seed(p, 200);
p.currentMoney = 5;
Seed(p, 200);
Check(p.currentMoney == 5, "metadata fallback does not replay native money");
SaveManager.CurrentRun.SetInt("Player0RerollDice", 5);
SaveManager.CurrentRun.SetString("SephiriaOne.Starting.v1.Player0.Dice", "broken");
Dice(p, 5);
p.rerollDice = 1;
Dice(p, 5);
Check(p.rerollDice == 1, "metadata fallback does not replay native dice");

// A saved authoritative restore can fail after a native write just like a grant.
Reset();
p = NewPlayer();
SaveManager.CurrentRun.SetInt("Player0Money", 10);
SaveManager.CurrentRun.SetString("SephiriaOne.Starting.v1.Player0.MoneyPending", "0|10");
int restoreWrites = 0;
p.OnMoneyWrite = _ => { if (++restoreWrites == 1) throw new Exception("restore wrote then failed"); };
Seed(p, 10);
Check(p.currentMoney == 10 && restoreWrites == 1, "uncertain authoritative restore is never repeated by its catch handler");

// A partial unload must remain quarantined across an addon reload.
Reset();
p = NewPlayer();
ResourceRuntime.Policy[ResourceKind.Leaves] = new(ResourceMode.Set, 20);
Seed(p, 200);
p.currentMoney = 5;
p.OnMoneyWrite = _ => throw new Exception("unload wrote then failed");
Check(UninstallFails(), "ambiguous unload refuses to remove starting hooks");
Check(p.currentMoney == 185, "first uncertain unload writes at most one refund");
p.OnMoneyWrite = null;
Enable();
Check(UninstallFails(), "ambiguous unload remains blocked after retry");
Check(p.currentMoney == 185, "unload refund cannot repeat after reload");

// Observing an uninitialized avatar at unload must not create fake grant history.
Reset();
p = NewPlayer();
StartingResourceHooks.Uninstall();
Enable();
ResourceRuntime.Policy[ResourceKind.Dice] = new(ResourceMode.Offset, 2);
Dice(p, 3);
Check(p.rerollDice == 5, "uninitialized unload does not poison future fresh dice grant");

// Fresh native TMP files have no SaveVersion until the first native save.
// Their legacy fallback reads must not turn host grants into guest restores.
Reset();
SaveManager.CurrentRun.Data.Remove("SaveVersion");
p = NewPlayer();
ResourceRuntime.Policy[ResourceKind.Leaves] = new(ResourceMode.Set, 20);
ResourceRuntime.Policy[ResourceKind.Dice] = new(ResourceMode.Offset, 2);
Seed(p, 200);
Dice(p, 3);
q = NewPlayer();
q.spawner.currentPlayerIdxForSave = 1;
q.spawner.playerGuid = "two";
Seed(q, SaveManager.CurrentRun.GetInt("PlayerMoney", 200));
Dice(q, SaveManager.CurrentRun.GetInt("PlayerRerollDice", 3));
Check(q.currentMoney == 20 && q.rerollDice == 5 && StartingResourceHooks.Available,
    "unversioned fresh host and guest receive independent grants");
Check(!SaveManager.CurrentRun.ContainsKey("PlayerMoney") && !SaveManager.CurrentRun.ContainsKey("PlayerRerollDice"),
    "fresh grants do not create shared legacy balance keys");
q.currentMoney = 5;
q.rerollDice = 0;
Disconnect(q);
var reconnect = NewPlayer();
reconnect.spawner.currentPlayerIdxForSave = 1;
reconnect.spawner.playerGuid = "two";
Seed(reconnect, 200);
Dice(reconnect, 3);
Check(reconnect.currentMoney == 5 && reconnect.rerollDice == 0,
    "unversioned town reconnect restores the per-player balance before native version migration");
SaveManager.CurrentRun.SetInt("SaveVersion", 2);
reconnect.Bonus = 100;
Depart(reconnect);
Check(reconnect.currentMoney == 5 && StartingResourceHooks.Available,
    "native save-version migration preserves frozen starting allocation and identity");

Console.WriteLine($"Passed {checks} real starting-hook lifecycle fixture checks.");
