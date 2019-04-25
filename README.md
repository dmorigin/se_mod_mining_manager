# Mining Manager
Author: DMOrigin

Page: https://www.gamers-shell.de/

This is a script for a programmable block in Space Engineers. It control and manage all operations of your little mining station. This includes Pistons, Rotor, Survival Kit, Drills and Inventory.

# Space Engineers

Space Engineers is a Game developed by Keen Software House. You can find more details about Keen Software House at his official 
Web Site at https://www.keenswh.com/. Or visit his forum directly at https://forums.keenswh.com/.

## Features

### Automatic shutdown

The script checks the fill ratio of all available inventory. If the fill ratio is greater or equal 95% this script will stop mining automaticaly. There is no need to interact with the script. Also, this script will restart automaticaly after the fill ratio is less or equal 70%.

### Automatic Settings

After the first start, this script beginns to search all the needed blocks. This includes a rotor for your drills, the drills himself and all your pistons. There is no need to mark the pistons that they looking up or down. The velocity of all your pistons and the rotation speed of your rotor will be setup by this script. You don't need to setup anything. If you have a Survival Kit on board, this script will generate a job to process the stones.

### Survival Kit

This is an optional block. If you don't have a SV Kit placed, the script will anounce that. If you have one, then this script generate a job to process all your stones. So all the ingots will be extracted from the stone.

## Parameters

This script supports three parameters

* -start: With this parameter the process of mining can be started. If you start this script the first time, it is in a "Stopped" state. So you need to start the mining process manualy. Also you need to do this if you stop the process.

* -stop: This stop the hole mining process. After this command the mining will not start automaticaly. You need to execute this script with the -start parameter.

* -reset: This makes to thinks. First, it stops the mining process like with the parameter -stop. Second, the drills will be pushing up to the start position.
