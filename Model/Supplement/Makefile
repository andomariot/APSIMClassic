include $(APSIM)/Model/Build/Platform.make

PROJECT = SupplementScienceConverter

SRC = SupplementConverter.cpp SupplementScienceConverter.cpp
LIBS = General ApsimShared ComponentInterface
ifeq ($(PLATFORM),Linux)
OBJS = $(APSIM)/Model/ComponentInterface/EntryPoints.o
else
OBJS = $(APSIM)/Model/ComponentInterface/EntryPoints.obj
endif

PROJECTTYPE = dll

include $(APSIM)/Model/Build/$(PLATFORM)CPP.make


