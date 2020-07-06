DESCRIPTION
-----------

This is a command-line tool to recursively add files to a VC++ project.

It takes in a list of folders to add, a project file, and recursively sync the files to the project.
While new files are added, missing files are pruned.
It appends all changes to a new `ItemGroup` in the project file, so that git diffs are more manageable.

It works both on `.vcxproj` and `.vcxproj.filters`. 
If a file was manually moved under a particular filter that isn't touched, unless the file itself has moved.

All paths are written relative to the project file.


USAGE
------

```
add-to-vcxproj [--help] --add [<paths>...] --to <project>
```

OPTIONS
-------

--add, -d [<paths>...]
					  Add a directory.
--to <project>        VC++ Project file (project.vcxproj.filters)
--help                display this list of options.

EXAMPLES
------------------

```
add-to-vcxproj --add src --to node.vcxproj
add-to-vcxproj --add src --to node.vcxproj.filters
```

For multiple folders, repeat the `--add` argument:

```
add-to-vcxproj --add src --add lib --to node.vcxproj
```

Since a sync is performed, it is safe to repeatedly execute this command.