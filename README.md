# Descripcion de la arquitectura de software

ESte repoisitorio contiene la documentacion de la arquiteturra del sistema...

## Objetivo
Aprender a trabajar la documentacion tecnica usando un flujo *docs-as-code*:
- Texto plano **AsciiDoc + Asciidoctor**.

- Control de verisones con **Git/Github o GitLab**
- Publicacion automatica de HTML y PDF con **CI/CD**
- Inclusion de diagramas creadas **Enterprise Architect**

## Estructuras del proyecto

docs/ -> Documentacion en AsciiDoc
images/ -> Diagramas exportados desde EA
build/ -> Salida generada (ignorada en Git)

## Requisitos
- Ruby (con Asciidoctor PDF instalados)
- VS Code con el plugin [AsciiDoctor  by Asciidoctor].


## Compilacion local (bash)
asciidoctor -D build docs/indice.adoc
asciidoctor-pdf -o build/indice.pdf docs/indice.adoc

