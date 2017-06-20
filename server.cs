function RTB_registerPref(%_, %_, %var, %_, %_, %val)
{
    eval(%var @ "=%val;");
}

datablock AudioDescription(AudioBorgSpeak3D : AudioDefault3D)
{
	volume = 0.8;
};

datablock AudioProfile(borgSpeakSound1)
{
	fileName = "./borg_speak1.wav";
	description = AudioBorgSpeak3D;
	preload = 1;
};

datablock AudioProfile(borgSpeakSound2)
{
	fileName = "./borg_speak2.wav";
	description = AudioBorgSpeak3D;
	preload = 1;
};

datablock AudioProfile(borgSpeakSound3)
{
	fileName = "./borg_speak3.wav";
	description = AudioBorgSpeak3D;
	preload = 1;
};

datablock AudioProfile(borgSpeakSound4)
{
	fileName = "./borg_speak4.wav";
	description = AudioBorgSpeak3D;
	preload = 1;
};

datablock AudioProfile(BorgSpeakSound5)
{
	fileName = "./borg_speak5.wav";
	description = AudioBorgSpeak3D;
	preload = 1;
};

datablock PlayerData(PlayerBorgArmor : PlayerNoJet)
{
	uiName = "";
	isBorg = 1;

	airControl = 0.4;
	
	maxTools = 0;
	maxDamage = PlayerStandardArmor.maxDamage + 25; // 35
	
	maxForwardSpeed = PlayerStandardArmor.maxForwardSpeed + 1.5;
	maxForwardCrouchSpeed = PlayerStandardArmor.maxForwardCrouchSpeed + 1.5;
	
	maxBackwardSpeed = PlayerStandardArmor.maxBackwardSpeed + 1.5;
	maxBackwardCrouchSpeed = PlayerStandardArmor.maxBackwardCrouchSpeed + 1.5;
	
	maxSideSpeed = PlayerStandardArmor.maxSideSpeed + 1.5;
	maxSideCrouchSpeed = PlayerStandardArmor.maxSideCrouchSpeed + 1.5;

	assimilateRange = 3;
};

function Player::becomeBorg(%this, %source)
{
	%client = %this.client;

	if (!isObject(%client))
	{
		return;
	}

	//serverCmdAlarm(%client);

	%this.clearTools();
	%this.unmountImage(0);

	%this.playThread(0, "root");
	%this.playThread(1, "root");

	%this.setDataBlock(PlayerBorgArmor);

	if (isObject(%source))
	{
		%miniGame = getMiniGameFromObject(%this);

		if (isObject(%miniGame))
		{
			%miniGame.schedule(0, checkLastManStanding);
		}

		// Only play sound when assimilated
		%sound = nameToID("BorgSpeakSound" @ getRandom(1, 5));

		if (isObject(%sound))
		{
			%this.playAudio(0, %sound);
		}

		%message = "\c3" @ %source.getPlayerName() @ " \c6has assimilated \c3" @ %client.getPlayerName() @ "\c6.";

		for (%i = 0; %i < %miniGame.numMembers; %i++)
		{
			%member = %miniGame.member[%i];

			if (%member == %client || %member == %source)
			{
				continue;
			}
			
			if (!isObject(%member.player) || !%member.player.getDataBlock().isBorg)
			{
				continue;
			}

			%member.chatMessage(%message);
		}
		
		%client.chatMessage("\c3" @ %source.getPlayerName() @ " \c6has assimilated you! Use the \c3Jet Button \c6to infect others.");
		%source.chatMessage("\c6You have assimilated \c3" @ %client.getPlayerName() @ "\c6!");
	}
}

function PlayerBorgArmor::onTrigger(%this, %obj, %slot, %state)
{
	%client = %obj.client;

	if (%slot != 4 || !%state || !isObject(%client))
	{
		Parent::onTrigger(%this, %obj, %slot, %state);
		return;
	}

	%obj.playThread(0, "activate2");

	%start = %obj.getEyePoint();
	%end = vectorAdd(%start, vectorScale(%obj.getEyeVector(), %this.assimilateRange));

	%mask = $TypeMasks::PlayerObjectType | $TypeMasks::fxBrickObjectType;
	%ray = containerRayCast(%start, %end, %mask, %obj);

	if (!%ray || !(%ray.getType() & $TypeMasks::PlayerObjectType))
	{
		return;
	}

	if (!isObject(%ray.client))
	{
		return;
	}

	if (%ray.getDataBlock().isBorg)
	{
		%client.centerPrint("\c6This player is already a \c3Borg\c6.", 1.75);
		return;
	}

	if (isEventPending(%ray.loopAssimilation))
	{
		%client.centerPrint("\c6This player is already being assimilated.", 1.75);
		return;
	}
	
	%ray.loopAssimilation(%obj.client, 0);
}

function Player::loopAssimilation(%this, %source, %ticks)
{
	cancel(%this.loopAssimilation);

	if (
		!isObject(%this.client) || %this.getState() $= "Dead" ||
		!isObject(%source.player) || %source.player.getState() $= "Dead" ||
		vectorDist(%this.getPosition(), %source.player.getPosition()) >= 5
	)
	{
		%source.player.isAssimilating = "";

		if (isObject(%this.client))
		{
			%this.client.setControlObject(%this);
		}

		if (isObject(%source.player))
		{
			%source.setControlObject(%source.player);
		}

		return;
	}

	if (!%ticks)
	{
		%source.player.isAssimilating = 1;
		serverCmdSit(%this.client);

		%this.client.camera.setMode("Corpse", %this);
		%this.client.setControlObject(%this.client.camera);

		%source.camera.setMode("Corpse", %source.player);
		%source.setControlObject(%source.camera);
	}

	if (%ticks >= 20)
	{
		%this.becomeBorg(%source);

		%source.player.isAssimilating = "";

		%this.client.setControlObject(%this);
		%source.setControlObject(%source.player);

		return;
	}

	%this.loopAssimilation = %this.schedule(100, loopAssimilation, %source, %ticks + 1);
}

package BorgPackage
{
	function MiniGameSO::addMember(%this, %client)
	{
		%before = %this.numMembers;
		Parent::addMember(%this, %client);

		if (%this.owner == 0 && !%before && %this.numMembers)
		{
			%this.reset(0);
		}
	}

	function MiniGameSO::reset(%this, %client)
	{
		Parent::reset(%this, %client);

		if (%this.owner == 0 && %this.numMembers)
		{
			%count = mFloor(%this.numMembers / 5) + 1;
			messageAll('', "\c3" @ %count @ " \c5random player" @ (%count == 1 ? " has" : "s have") @ " been chosen to be the first \c3Borg" @ (%count == 1 ? "" : "s") @ "\c5." );

			for (%i = 0; %i < %this.numMembers; %i++)
			{
				if (!isObject(%this.member[%i].player))
				{
					continue;
				}

				%valid = %valid @ (%i ? " " : "") @ %this.member[%i];

				//%this.member[%i].player.setShapeNameDistance(0);
				%this.member[%i].player.setShapeName("", "8564862");
			}

			for (%i = 0; %i < %count; %i++)
			{
				%index = getRandom(0, getWordCount(%valid) - 1);

				%member = getWord(%valid, %index);
				%valid = removeWord(%valid, %index);

				%member.player.becomeBorg(0);
				%member.chatMessage("\c6You were selected to be one of the first \c3Borgs\c6! Use the \c3Jet Button \c6to assimilate other players.");
			}
		}
	}

	function servercmdmessagesent(%a,%b)
	{
		parent::servercmdmessagesent(%a,%b);
		//if(isobject(%a.player))
		//	%a.player.setshapename(%b,"8564862");
	}

	function MiniGameSO::checkLastManStanding(%this)
	{
		if (%this.owner != 0)
		{
			return Parent::checkLastManStanding(%this);
		}

		if (!%this.numMembers || isEventPending(%this.resetSchedule))
		{
			return 0;
		}

		for (%i = 0; %i < %this.numMembers; %i++)
		{
			if (isObject(%this.member[%i].player) && %this.member[%i].player.getState() !$= "Dead")
			{
				%alive[%this.member[%i].player.getDataBlock().isBorg ? 1 : 0]++;
			}
		}

		if (!%alive0 && %alive1)
		{
			messageAll('', "\c3The Borgs \c5won this round.");
		}
		else if (%alive0 && !%alive1)
		{
			messageAll('', "\c3The Blockheads \c5won this round.");
		}
		else if (!%alive0 && !%alive1)
		{
			messageAll('', "\c5Nobody won this round.");
		}
		else
		{
			return 0;
		}

		%this.scheduleReset();
		return 0;
	}

	function Player::damage(%this, %source, %position, %damage, %type)
	{
		if (isObject(%source) && %source != %this)
		{
			if (!%this.getDataBlock().isBorg && !%source.getDataBlock().isBorg)
			{
				return;
			}
			
			if (%this.getDataBlock().isBorg && %source.getDatablock().isBorg)
			{
				return;
			}
		}
		
		Parent::damage(%this, %source, %position, %damage, %type);
	}

	function Player::mountImage(%this, %image, %slot)
	{
		%allow["PlayerTeleportImage"] = 1;
		%allow["PainLowImage"] = 1;
		%allow["PainMidImage"] = 1;
		%allow["PainHighImage"] = 1;

		if (!%this.getDataBlock().isBorg || %allow[%image.getName()])
		{
			Parent::mountImage(%this, %image, %slot);
		}
	}
	
	function Player::playThread(%this, %slot, %thread)
	{
		if (!%this.getDataBlock().isBorg || %slot != 1)
		{
			Parent::playThread(%this, %slot, %thread);
		}
	}

	function Observer::onTrigger(%this, %obj, %slot, %state)
	{
		%player = %obj.getControllingClient().player;

		if (
			!isEventPending(%player.loopAssimilation) &&
			!%player.isAssimilating
		)
		{
			Parent::onTrigger(%this, %obj, %slot, %state);
		}
	}
};

activatePackage("BorgPackage");
