﻿
Set-OctopusVariable -Name "TestA" -Value "World!"

Set-OctopusVariable -Name "ThisIsAVeryLongVariableNameWithMoreThan33CharactersIThinkThisIsAVeryLongVariableNameWithMoreThan33CharactersIThinkThisIsAVeryLongVariableNameWithMoreThan33CharactersIThinkThisIsAVeryLongVariableNameWithMoreThan33CharactersIThink" -Value "World!"

Set-OctopusVariable -Name "TestB" -Value "This is a really really really really really really really really really really really really really really really really really really really really really really really long string!"

Set-OctopusVariable -Name "TestC" -Value "Hello?!@CW)*F@!(#*DDOLDSKC<>'"

$x = Set-OctopusVariable -Name "TestD" -Value "Hello"
