Note
Une note locale, légère et toujours à portée de main.
A local, light note app, always within reach.
=========================================================

============================== EN ==============================

Overview
--------
Note is a small floating Windows notepad. It stays above other
windows, autosaves your text, and keeps your data on your machine.
No account, no server, no syncing.

Features
--------
- multiple renamable note sheets;
- automatic save;
- window always visible above other apps;
- easy move and resize;
- minimalist themes, including a custom color;
- adjustable text size;
- global shortcut to show or hide the window quickly;
- launch-with-Windows option;
- local trash limited to the last deleted sheets.

Installation
------------
1. Extract the folder to a stable location, e.g. C:\Apps\Note\.
2. Keep notes.exe, notes.xaml and notes.ico together in that folder.
3. Double-click notes.exe.
4. Optional: pin the app to the taskbar.

Usage
-----
- use the + button to add a sheet;
- double-click a tab to rename;
- right-click a tab to rename or delete;
- drag the top bar to move the window;
- resize from the edges;
- open settings via the gear icon;
- use the global shortcut (Ctrl+Alt+N by default) to hide or
  recall Note quickly.

The window's close button hides the app without quitting it. To
fully quit Note, use the "Quit Note" button in the Information
tab of the settings.

Local data
----------
Notes, settings, themes, window position and trash items are stored
locally in:
%APPDATA%\Note\

Note creates no account, sends no notes to any server, and syncs
nothing.

Writes are atomic: Note first writes to a temporary file then
replaces the main file, avoiding corruption if a crash happens
during a save.

Trash
-----
Deleted sheets go to a local trash limited to the last three items.
From the settings you can restore a sheet, permanently delete an
item, or empty the trash. Permanent deletion removes the item from
the data the app maintains.

Privacy
-------
Note is designed to stay on your computer. No telemetry, no
tracking, no remote service.

Uninstall
---------
If you installed Note via install_note.exe:
- open Windows Settings > Apps > Installed apps, search "Note",
  click Uninstall;
- or run uninstall_note.exe directly from the install folder.

The "Also delete my notes" option in the uninstaller will also
remove %APPDATA%\Note\.

If you used the zip:
1. delete the folder containing the app;
2. delete %APPDATA%\Note\ to remove local data;
3. if "Launch with Windows" was enabled, remove the Note entry
   in HKCU\Software\Microsoft\Windows\CurrentVersion\Run.

Author
------
Free and open-source app by Le Geai Informatique.
https://legeai-informatique.fr


============================== FR ==============================

Présentation
------------
Note est une petite application Windows de prise de notes flottante.
Elle reste au-dessus des autres fenêtres, sauvegarde automatiquement
votre texte, et conserve vos données sur votre machine. Aucun compte,
aucun serveur, aucune synchronisation.

Fonctionnalités
---------------
- plusieurs feuilles de notes renommables ;
- sauvegarde automatique ;
- fenêtre toujours visible au-dessus des autres applications ;
- déplacement et redimensionnement simples ;
- thèmes sobres, dont une couleur personnalisée ;
- taille du texte réglable ;
- raccourci global pour afficher ou masquer rapidement la fenêtre ;
- option de lancement avec Windows ;
- corbeille locale limitée aux dernières feuilles supprimées.

Installation
------------
1. Extraire le dossier dans un emplacement stable, par exemple
   C:\Apps\Note\.
2. Garder notes.exe, notes.xaml et notes.ico ensemble dans ce dossier.
3. Double-cliquer sur notes.exe.
4. Optionnel : épingler l'application à la barre des tâches.

Utilisation
-----------
- utilisez le bouton + pour ajouter une feuille ;
- double-cliquez sur un onglet pour le renommer ;
- clic droit sur un onglet pour renommer ou supprimer ;
- glissez le bandeau supérieur pour déplacer la fenêtre ;
- redimensionnez depuis les bords ;
- ouvrez les paramètres avec l'engrenage ;
- utilisez le raccourci global (Ctrl+Alt+N par défaut) pour masquer
  ou rappeler rapidement Note.

La croix de la fenêtre masque l'application sans la fermer. Pour
vraiment quitter Note, utilisez le bouton « Quitter Note » dans
l'onglet Information des paramètres.

Données locales
---------------
Les notes, paramètres, thèmes, position de fenêtre et éléments de
corbeille sont stockés localement dans :
%APPDATA%\Note\

Note ne crée pas de compte, n'envoie pas vos notes sur un serveur,
et ne synchronise rien.

L'écriture est atomique : Note écrit d'abord dans un fichier
temporaire puis remplace le fichier principal, ce qui évite les
corruptions en cas de plantage pendant la sauvegarde.

Corbeille
---------
Les feuilles supprimées sont placées dans une corbeille locale
limitée aux trois derniers éléments. Depuis les paramètres, vous
pouvez restaurer une feuille, supprimer définitivement un élément,
ou vider la corbeille. La suppression définitive retire l'élément
des données maintenues par l'application.

Confidentialité
---------------
Note est conçu pour rester sur votre ordinateur. Aucune télémétrie,
aucun suivi, aucun service distant.

Désinstallation
---------------
Si vous avez installé Note via install_note.exe :
- ouvrir Paramètres Windows > Applications > Applications installées,
  rechercher « Note », cliquer sur Désinstaller ;
- ou exécuter directement uninstall_note.exe dans le dossier
  d'installation.

L'option « Supprimer aussi mes notes » dans le désinstalleur permet
d'effacer également %APPDATA%\Note\.

Si vous avez utilisé le zip :
1. supprimer le dossier contenant l'application ;
2. supprimer %APPDATA%\Note\ pour effacer les données locales ;
3. si « Lancer avec Windows » avait été activé, retirer l'entrée
   Note dans HKCU\Software\Microsoft\Windows\CurrentVersion\Run.

Auteur
------
Application gratuite et open source réalisée par Le Geai Informatique.
https://legeai-informatique.fr
